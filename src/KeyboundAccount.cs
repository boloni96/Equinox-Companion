using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool eventAccountActive,eventAccountWorldOwned,eventAccountMoveOwned,eventAccountBuyingStarted,eventAccountFarmStarted;
    private string eventAccountStatus="Choose characters from this service account.",eventAccountPairing="",eventAccountPerson="",eventAccountId="",eventAccountStage="";
    private readonly HashSet<string> eventAccountSelected=[];
    private SharedCharacter[] eventAccountCharacters=[];
    private int eventAccountIndex,eventAccountCompleted,eventAccountSkipped;
    private ulong eventAccountLoadedCharacter;
    private DateTimeOffset eventAccountReadyAt;
    private DateTimeOffset eventAccountNext,eventAccountStageAt,eventAccountActionAt,eventAccountDeadline;
    private uint eventAccountPathTerritory;
    private Vector3 eventAccountGoal;
    private bool eventEntranceSceneOwned;
    private DateTimeOffset eventEntranceInteractionAt;
    private SharedCharacter[] EventAccountCandidates(){
        var current=CurrentSharedCharacter();if(current==null||string.IsNullOrEmpty(current.AccountId))return [];
        var person=(config.SharedRoster?.People??[]).SingleOrDefault(p=>p.Characters.Any(c=>c.Id==current.Id));
        return person?.Characters.Where(c=>c.AccountId==current.AccountId&&!string.IsNullOrEmpty(c.ContentId)).DistinctBy(c=>c.Id).ToArray()??[];
    }
    private void DrawEventAccount(){
        if(!ImGui.CollapsingHeader("Account event routine"))return;
        if(eventAccountActive){
            ImGui.TextWrapped(eventAccountStatus);
            if(ImGui.Button("Stop account routine"))StopEventAccount("Stopped by you.");
            return;
        }
        var candidates=EventAccountCandidates();
        if(ImGui.SmallButton("Select all"))foreach(var c in candidates)eventAccountSelected.Add(c.Id);
        ImGui.SameLine();if(ImGui.SmallButton("Clear all"))eventAccountSelected.Clear();
        if(ImGui.BeginChild("EventAccountCharacters",new Vector2(0,110),true)){
            foreach(var c in candidates){
                bool selected=eventAccountSelected.Contains(c.Id);
                if(ImGui.Checkbox(c.Name+" @ "+c.World+"##"+c.Id,ref selected)){
                    if(selected)eventAccountSelected.Add(c.Id);else eventAccountSelected.Remove(c.Id);
                }
            }
        }ImGui.EndChild();
        ImGui.BeginDisabled(candidates.All(c=>!eventAccountSelected.Contains(c.Id)));
        if(ImGui.Button("Start selected characters"))StartEventAccount(candidates.Where(c=>eventAccountSelected.Contains(c.Id)).ToArray());
        ImGui.EndDisabled();
        ImGui.TextWrapped(eventAccountStatus);
    }
    private void EventAccountStage(string stage,string message){
        if(nativeEventActive&&eventAccountStage=="quest"){eventAccountStatus=message;return;}
        if(eventAccountStage!=stage){eventAccountStage=stage;eventAccountStageAt=DateTimeOffset.UtcNow;eventAccountActionAt=default;}
        eventAccountStatus=message;
    }
    private void StartEventAccount(SharedCharacter[] characters){
        try{
            if(!config.EnableKeyboundBrawler||!Player.IsLoaded||characters.Length==0)throw new InvalidOperationException("Enable the event tool and log in first.");
            if(nativeEventActive||questChainActive||questRecorderActive||questionableOwnedQuest!=0||QuestToolsOtherOperation()||assignedLogin!=null)throw new InvalidOperationException("Stop other automation before starting the account routine.");
            if(Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.InDutyQueue])throw new InvalidOperationException("Leave the duty, queue or combat first.");
            var current=CurrentSharedCharacter()??throw new InvalidOperationException("Waiting for this character's Journal identity.");
            var scope=CharacterSwitchScope(current.Id);
            if(scope.PersonId.Length==0||scope.AccountId.Length==0)throw new InvalidOperationException("Waiting for this character's Person and account from the Journal.");
            if(characters.Any(c=>c.AccountId!=current.AccountId||!EventAccountCandidates().Any(v=>v.Id==c.Id)))throw new InvalidOperationException("Selected characters no longer belong to this account.");
            if(LifestreamBusy()||OptionalQuestRunning())throw new InvalidOperationException("A travel or quest plugin is busy.");
            eventAccountLoadedCharacter=0;eventAccountCompleted=eventAccountSkipped=0;eventAccountCharacters=characters.OrderBy(c=>c.Id==current.Id?0:1).ToArray();eventAccountIndex=0;
            eventAccountPairing=config.PairingKey;eventAccountPerson=scope.PersonId;eventAccountId=scope.AccountId;
            eventAccountActive=true;eventAccountDeadline=DateTimeOffset.UtcNow.AddHours(12);eventAccountNext=default;eventAccountStage="";
            eventAccountBuyingStarted=eventAccountFarmStarted=false;EventAccountStage("character","Checking selected character");
        }catch(Exception e){eventAccountStatus="Not started: "+e.Message;}
    }
    private void StopEventAccount(string reason){
        if(!eventAccountActive)return;
        eventAccountActive=false;eventEntranceSceneOwned=false;eventEntranceInteractionAt=default;StopNativeEventQuest(reason);CancelEventAccountMovement();
        if(eventAccountWorldOwned){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}eventAccountWorldOwned=false;}
        if(assignedLogin!=null)CancelAssignedLogin(reason);
        StopQuestionableQuest(reason);StopKeybound(reason);if(keyboundBuying)StopKeyboundBuying(reason);
        eventAccountStatus=reason;PrintEquinox("Event routine · "+reason);
    }
    private unsafe bool EventAccountSquare(DateTimeOffset now){
        if(Client.TerritoryType==1360){eventEntranceSceneOwned=false;eventEntranceInteractionAt=default;return true;}
        if(!EventReachSaucer(now)||!EventReachEntrance(now))return false;
        EventAccountStage("entrance","Going to the Keybound Square entrance");
        var npc=Objects.FirstOrDefault(o=>o.BaseId==1058754&&o.IsTargetable);
        var goal=npc?.Position??new Vector3(.804425f,3.98254f,61.2062f);
        if(VisibleFollowAddon("SelectString")||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("SelectIconString")){
            var place=DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(1360)?.PlaceName.Value.Name.ToString()??"";
            if(place.Length>0&&eventEntranceSceneOwned&&now-eventEntranceInteractionAt<TimeSpan.FromMinutes(2)&&now-eventAccountActionAt>TimeSpan.FromSeconds(2)){
                foreach(var name in new[]{"SelectString","SelectIconString"}){
                    var addon=KeyboundAddon(name);var choices=HelperChoices(addon,name);
                    var matches=choices.Select((text,index)=>(text,index)).Where(x=>x.text.Contains(place,StringComparison.OrdinalIgnoreCase)).ToArray();
                    if(matches.Length==1){SelectTravelChoice(addon,matches[0].index);eventAccountActionAt=now;return false;}
                }
            }
            eventAccountStatus="Entrance choice needs verification; select Keybound Square manually. The routine resumes on arrival.";
            return false;
        }
        if(VisibleFollowAddon("Talk")){
            if(eventEntranceSceneOwned&&now-eventEntranceInteractionAt<TimeSpan.FromMinutes(2)&&now-eventAccountActionAt>TimeSpan.FromSeconds(1)){ClickVisibleTalk();eventAccountActionAt=now;}
            return false;
        }
        if(Conditions[ConditionFlag.OccupiedInEvent])return false;
        if(!EventAccountWalk(goal,2.5f,now)||npc==null)return false;
        if(now-eventAccountActionAt<TimeSpan.FromSeconds(3))return false;
        var system=TargetSystem.Instance();if(system==null)return false;
        eventEntranceSceneOwned=false;eventEntranceInteractionAt=now;
        Targets.Target=npc;system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)npc.Address,false);eventAccountActionAt=now;
        return false;
    }
    private unsafe void UpdateEventAccount(DateTimeOffset now){
        if(!eventAccountActive||now<eventAccountNext)return;eventAccountNext=now.AddMilliseconds(500);
        try{
            if(config.PairingKey!=eventAccountPairing||!config.EnableKeyboundBrawler)throw new InvalidOperationException("Pairing or event tool changed.");
            if(now>eventAccountDeadline)throw new InvalidOperationException("Account routine reached its 12-hour limit.");
            if(eventAccountIndex>=eventAccountCharacters.Length){CelebrateEventAccount();StopEventAccount($"Finished: {eventAccountCompleted} character(s) completed their reward goals; {eventAccountSkipped} unavailable character(s) skipped.");return;}
            var target=eventAccountCharacters[eventAccountIndex];
            if(assignedLogin!=null){eventAccountStatus=characterTravelStatus;return;}
            if(eventAccountWorldOwned){
                if(now-eventAccountActionAt>TimeSpan.FromMinutes(30))throw new InvalidOperationException("Sophia travel timed out.");
                eventAccountStatus="Travelling to Sophia · waiting for verified arrival";
                if(LifestreamBusy())return;
                if(!Player.IsLoaded||now-eventAccountActionAt<TimeSpan.FromSeconds(5))return;
                eventAccountWorldOwned=false;
                if(Player.CurrentWorld.Value.Name.ToString()!="Sophia")throw new InvalidOperationException("Travel ended before reaching Sophia.");
            }
            if(!Player.IsLoaded)return;
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
            var current=CurrentSharedCharacter()??throw new InvalidOperationException("Current character identity is not synced.");
            var scope=CharacterSwitchScope(current.Id);
            if(scope.PersonId!=eventAccountPerson||scope.AccountId!=eventAccountId)throw new InvalidOperationException("Current character is outside the reserved account.");
            if(!QuestToolsPolicy.EventCharacterReady(current.Id,target.Id,CurrentLoginServiceAccount())){
                CancelEventAccountMovement();
                EventAccountStage("accountcheck","Waiting before switching to "+target.Name+". The login service-account slot is not known. Log into that character manually in this client; this routine resumes after login.");
                return;
            }
            if(current.Id!=target.Id){
                if(eventAccountStage=="switch")throw new InvalidOperationException("Character switch stopped before the selected character logged in. "+characterTravelStatus);
                EventAccountStage("switch","Switching to "+target.Name);
                if(!StartAssignedCharacterSwitch(target))throw new InvalidOperationException(characterTravelStatus);
                return;
            }
            if(eventAccountLoadedCharacter!=Player.ContentId){
                eventAccountLoadedCharacter=Player.ContentId;eventAccountReadyAt=now.AddSeconds(5);
                eventAccountStatus="Waiting for "+target.Name+"'s quest and inventory data";return;
            }
            var liveState=UIState.Instance();
            if(now<eventAccountReadyAt||liveState==null||!liveState->PlayerState.IsLoaded||QuestManager.Instance()==null)return;
            if(followSession.Armed||TraderRunning||gardenCareRun?.Running==true||gearRouting||armoireRunning||organizeRunning||retrievalRun.Running||sellerRun.Running)
                throw new InvalidOperationException("Another helper or inventory operation started.");
            if(FollowMovementKeysHeld()){StopEventAccount("Stopped by your movement; progress and purchases are kept.");return;}
            if(now-eventAccountStageAt>TimeSpan.FromMinutes(5)&&eventAccountStage is not ("farm" or "quest"))throw new InvalidOperationException("No confirmed progress: "+eventAccountStatus);
            if(Player.CurrentWorld.Value.Name.ToString()!="Sophia"){
                EventAccountStage("world","Travelling to Sophia");
                if(LifestreamBusy())return;
                var sophia=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().FirstOrDefault(w=>w.Name.ToString()=="Sophia");
                var cross=Player.CurrentWorld.Value.DataCenter.RowId!=sophia.DataCenter.RowId;
                if(!Pi.GetIpcSubscriber<string,bool>(cross?"Lifestream.CanVisitCrossDC":"Lifestream.CanVisitSameDC").InvokeFunc("Sophia"))throw new InvalidOperationException("Sophia travel is temporarily unavailable.");
                eventAccountWorldOwned=true;eventAccountActionAt=now;
                Pi.GetIpcSubscriber<string,bool,string,bool,int?,bool?,bool?,object>("Lifestream.TPAndChangeWorld").InvokeAction("Sophia",cross,"",true,null,false,false);
                return;
            }
            if(!QuestManager.IsQuestComplete(QuestToolsPolicy.KeywardBound)){
                if(nativeEventActive){eventAccountStatus=nativeEventStatus;return;}
                if(eventAccountStage=="quest")throw new InvalidOperationException(nativeEventStatus);
                EventAccountStage("quest","Completing Keyward Bound with Companion");
                if(!StartNativeEventQuest(true))throw new InvalidOperationException(nativeEventStatus);
                return;
            }
            if(nativeEventActive){eventAccountStatus=nativeEventStatus;return;}
            if(questionableOwnedQuest!=0)return;
            if(Client.TerritoryType==1359||Conditions[ConditionFlag.InDutyQueue]||Conditions[ConditionFlag.WaitingForDuty]||
                Conditions[ConditionFlag.WaitingForRaceOrMatch]||Conditions[ConditionFlag.RegisteringForRaceOrMatch]||Conditions[ConditionFlag.WaitingForDutyFinder]||VisibleFollowAddon("ContentsFinderConfirm")){
                eventAccountStatus="Finishing the current event round";return;
            }
            if(keyboundBuying){eventAccountStatus=keyboundBuyStatus;return;}
            if(eventAccountBuyingStarted){
                eventAccountBuyingStarted=false;
                RefreshKeyboundBuyPlan();
                if(keyboundMissingPlan==null||keyboundMissingPlan.Count>0||keyboundUseReady>0||keyboundHeldRewards.Count>0)
                    throw new InvalidOperationException("Purchases/use need attention: "+keyboundBuyStatus);
            }
            if(eventAccountFarmStarted&&!keyboundEnabled)throw new InvalidOperationException("Event farming stopped: "+keyboundStatus);
            RefreshKeyboundBuyPlan();
            var decision=QuestToolsPolicy.Next(true,keyboundMissingPlan!=null,keyboundMissingPlan==null?0:KeyboundShoppingPolicy.Total(keyboundMissingPlan),
                KeyboundWallet(),keyboundUseReady,keyboundHeldRewards.Count);
            if(decision==EventRoutineAction.Verify)throw new InvalidOperationException(keyboundPlanError.Length>0?keyboundPlanError:"Purchases are held for verification; no duplicate was bought.");
            if(decision==EventRoutineAction.Next){
                StopKeybound();eventAccountFarmStarted=false;CancelEventAccountMovement();
                PrintEquinox("Event routine · "+target.Name+": reward goal and item use confirmed.");
                eventAccountCompleted++;eventAccountIndex++;EventAccountStage("character","Preparing next selected character");return;
            }
            if(!EventAccountSquare(now))return;
            if(decision==EventRoutineAction.BuyAndUse){
                StopKeybound();eventAccountFarmStarted=false;EventAccountStage("buy","Going to the MGC trader");
                var trader=Objects.FirstOrDefault(KeyboundIsTrader);
                if(trader==null){eventAccountStatus="Waiting for the MGC trader to load";return;}
                if(!EventAccountWalk(trader.Position,trader.HitboxRadius+2.5f,now))return;
                StartKeyboundBuying(true);
                if(!keyboundBuying)throw new InvalidOperationException(keyboundBuyStatus);
                eventAccountBuyingStarted=true;return;
            }
            EventAccountStage("farm","Farming missing rewards · "+target.Name);
            if(keyboundEnabled){eventAccountStatus=keyboundStatus;return;}
            var registrar=Objects.FirstOrDefault(o=>o.BaseId==1058751&&o.IsTargetable);
            if(registrar==null){eventAccountStatus="Waiting for the Keybound registrar to load";return;}
            if(!EventAccountWalk(registrar.Position,registrar.HitboxRadius+2.5f,now))return;
            keyboundAutoRepeat=true;keyboundEnabled=true;keyboundNext=keyboundHold=keyboundMissing=default;
            keyboundStatus="Ready to register";eventAccountFarmStarted=true;
        }catch(Exception e){StopEventAccount("Stopped: "+e.Message);}
    }
}

