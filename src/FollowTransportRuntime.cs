using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private FollowPortalSignal? transportCapture,pendingTransport;
    private DateTimeOffset transportCaptureAt,transportNext,transportStarted,lastTransportChoiceAt;
    private int transportStep;
    private bool transportSawLoading;
    private string lastTransportChoice="";
    private readonly FollowNoticeGate travelDiagnostics=new();
    private void TravelDiagnostic(string message){if(portalRelayStatus!=message)RecordFollowTravel("Status",message);portalRelayStatus=message;if(travelDiagnostics.Changed(message))FollowChatNotice("TRAVEL — "+message);}
    private void PauseFollowForTravel(){followSession.Pause();RequestFollowMovementStop();followReady.Reset();}
    private unsafe void CaptureTransportSource(Dalamud.Game.ClientState.Objects.Types.IGameObject clicked)
    {
        transportCapture=null;transportSawLoading=false;
        if(!SharingTravel||usingSharedTravel||relayInteracting||clicked.ObjectKind is not (ObjectKind.Aetheryte or ObjectKind.EventNpc or ObjectKind.EventObj))return;
        var observed=TravelSignal("transport",0,"",clicked.BaseId,clicked.Position);if(observed==null)return;
        transportCapture=observed with {SourceKind=clicked.ObjectKind.ToString(),SourceRadius=Math.Clamp(clicked.HitboxRadius,0,10),Steps=[]};
        var housing=FFXIVClientStructs.FFXIV.Client.Game.HousingManager.Instance();
        if(clicked.ObjectKind==ObjectKind.EventObj&&clicked.Name.TextValue is "Entrance" or "Exit" or "Entrance to the Company Workshop" or "Entrance to Additional Chambers" or "Workshop Entrance"&&housing!=null&&housing->CurrentTerritory!=null)
            transportCapture=transportCapture with {TravelKind="door"};
        transportCaptureAt=DateTimeOffset.UtcNow;lastTransportChoice="";
    }
    private unsafe void CaptureTransportChoice(AtkUnitBase* addon,int index)
    {
        if(SharingTravel&&!usingSharedTravel&&transportCapture==null&&addon!=null&&addon==(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address&&Objects.LocalPlayer is {} local){
            var list=TransportChoices(addon);
            var housing=FFXIVClientStructs.FFXIV.Client.Game.HousingManager.Instance();
            if(index>=0&&index<list.Count&&list[index].Trim().TrimEnd('.')=="Leave residential district"&&housing!=null&&housing->GetCurrentHousingTerritoryType()==FFXIVClientStructs.FFXIV.Client.Game.HousingTerritoryType.Outdoor){
                if(TravelSignal("transport",0,"",0,local.Position) is {} captured)transportCapture=captured with {SourceKind="boundary",Steps=[]};transportSawLoading=false;transportCaptureAt=DateTimeOffset.UtcNow;
            }
        }
        if(!SharingTravel||usingSharedTravel||pendingTransport!=null||addon==null||transportCapture is not {} source||DateTimeOffset.UtcNow-transportCaptureAt>TimeSpan.FromSeconds(120)||index<0)return;
        string text="";bool confirmation=false;
        if(addon==(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address){
            var choices=TransportChoices(addon);if(index>=choices.Count)return;text=choices[index];
        }else if(addon==(AtkUnitBase*)GardenGui.GetAddonByName("SelectYesno").Address&&index==0){
            var yes=(AddonSelectYesno*)addon;if(yes->PromptText==null)return;text=yes->PromptText->NodeText.ToString();confirmation=true;
        }else return;
        if(!(source.TravelKind=="friendestate"?FollowTransportPolicy.EstateChoice(text):FollowTransportPolicy.StepSupported(text,confirmation))){transportCapture=null;if(source.Steps is {Length:>0})TravelDiagnostic("Unrecognized travel menu; this interaction remains manual.");return;}
        var now=DateTimeOffset.UtcNow;
        if(text==lastTransportChoice&&now-lastTransportChoiceAt<TimeSpan.FromMilliseconds(100))return;
        lastTransportChoice=text;lastTransportChoiceAt=now;
        var steps=source.Steps??[];if(steps.Length>=8){transportCapture=null;return;}
        transportCapture=source with {Steps=[..steps,new(text,confirmation)]};
    }
    private unsafe List<string> TransportChoices(AtkUnitBase* menu)
    {
        var result=new List<string>();
        if(menu==null||!menu->IsVisible)return result;
        var popup=&((AddonSelectString*)menu)->PopupMenu.PopupMenu;
        if(popup->EntryNames==null||popup->EntryCount is <1 or >32)return result;
        for(var i=0;i<popup->EntryCount;i++)result.Add(TravelMenuText(popup->EntryNames[i].Value)??"");
        return result;
    }
    private unsafe void UpdateFollowTransport(DateTimeOffset now)
    {
        if(transportCapture is {} capture){
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])transportSawLoading=true;
            if(!SharingTravel||now-transportCaptureAt>TimeSpan.FromSeconds(120))transportCapture=null;
            else if(Player.IsLoaded&&Player.CharacterName==capture.Name&&Player.HomeWorld.RowId==capture.HomeWorld&&(Client.TerritoryType!=capture.Territory||transportSawLoading)&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51]){
                transportCapture=null;
                // More specific native aethernet/ward capture takes precedence.
                if(outgoingTravel==null&&FollowTransportPolicy.Valid(capture)&&config.PairingKey.Length==64)
                    EnqueueOutgoingTravel(config.PairingKey,capture with {SentAt=now.ToUnixTimeMilliseconds()},portalRelay.HasFollowers(config.PairingKey,capture.Name,capture.HomeWorld));
            }
        }
        if(pendingTransport is not {} pending)return;
        if(!followSession.Armed||!config.EnableFollowThem||!config.FollowThem.UseSharedTeleports||!Player.IsLoaded||(Client.TerritoryType!=pending.Territory&&!(pending.TravelKind=="friendestate"&&config.FollowThem.MeetAtTeleports))||Player.CurrentWorld.RowId!=pending.CurrentWorld||Conditions[ConditionFlag.InCombat]||!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,pending.Name,pending.HomeWorld)){pendingTransport=null;return;}
        if(now-transportStarted>TimeSpan.FromSeconds(15)){pendingTransport=null;TravelDiagnostic("Transport timed out; menu or unlock differs. Waiting for your selected character.");return;}
        if(!travelStepReady)return;
        if(now<transportNext||pending.Steps==null)return;
        if(AdvanceTravelTalk(pending)){transportNext=now.AddMilliseconds(750);return;}
        RetryTravelInteraction(pending,now);
        if(transportStep>=pending.Steps.Length)return;
        var step=pending.Steps[transportStep];
        if(pending.TravelKind=="friendestate"&&!step.Confirmation&&step.Text is "Private Estate" or "Free Company Estate"){
            transportNext=now.AddSeconds(1);
            if(!TrySelectFriendEstate(pending,step.Text))return;
            transportStep++;transportNext=now.AddSeconds(1);
            if(transportStep>=pending.Steps.Length){pendingTransport=null;TravelDiagnostic("Estate row selected; waiting for departure. If a confirmation remains open, confirm it manually for this test.");}
            return;
        }
        if(step.Addon.Length>0){
            if(ReplayFollowRoom(step)){transportStep++;if(transportStep>=pending.Steps.Length)pendingTransport=null;transportNext=now.AddMilliseconds(750);}
            return;
        }
        AtkUnitBase* menu=null;int index=-1;
        if(step.Confirmation){
            var yes=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
            if(yes==null||!yes->IsVisible||yes->PromptText==null)return;
            if(yes->PromptText->NodeText.ToString()!=step.Text){pendingTransport=null;TravelDiagnostic("Transport confirmation differs; not accepted.");return;}menu=(AtkUnitBase*)yes;index=0;
        }else{
            menu=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address;var choices=TransportChoices(menu);
            if(choices.Count==0)return;
            if(choices.Count(x=>x.Trim().TrimEnd('.')==step.Text.Trim().TrimEnd('.'))!=1){TravelDiagnostic("Waiting for the recorded transport menu choice to appear.");return;}index=choices.FindIndex(x=>x.Trim().TrimEnd('.')==step.Text.Trim().TrimEnd('.'));
        }
        if(!FollowTransportPolicy.Affordable(step.Text,config.FollowThem.TeleportGilLimit)){pendingTransport=null;TravelDiagnostic("Transport fee is unknown or exceeds your gil limit; waiting.");return;}
        transportStep++;if(transportStep>=pending.Steps.Length)pendingTransport=null;
        transportNext=now.AddMilliseconds(750);usingSharedTravel=true;try{SelectTravelChoice(menu,index);}finally{usingSharedTravel=false;}
        TravelDiagnostic("Requested transport: "+step.Text);
    }
    private unsafe void TryFollowTransport(FollowPortalSignal signal,DateTimeOffset now)
    {
        if(!FollowTransportPolicy.Valid(signal)||Objects.LocalPlayer is not {} self)return;
        if(signal.TravelKind=="friendestate"){
            PauseFollowForTravel();
            if(!OpenSharedFriendEstate(signal)){TravelDiagnostic("Friend estate unavailable: open your Friends List to refresh it, and confirm this person is your friend with estate teleport enabled.");return;}
            lastPortalSignalId=signal.Id;pendingTransport=signal;transportStep=0;transportStarted=now;transportNext=now.AddMilliseconds(500);return;
        }
        if(signal.SourceKind=="boundary"){
            if(!MatchingTravelMenu(signal)){TravelDiagnostic("Waiting for the residential exit menu at the recorded entrance.");return;}
            lastPortalSignalId=signal.Id;pendingTransport=signal;transportStep=0;transportStarted=now;transportNext=now.AddMilliseconds(500);return;
        }
        var source=Objects.FirstOrDefault(x=>x.ObjectKind.ToString()==signal.SourceKind&&x.BaseId==signal.BaseId&&x.IsTargetable&&Vector3.Distance(x.Position,new(signal.X,signal.Y,signal.Z))<1&&Vector3.Distance(x.Position,self.Position)<=x.HitboxRadius+3);
        if(source==null){TravelDiagnostic("Move beside the same transport NPC or crystal; waiting.");return;}
        lastPortalSignalId=signal.Id;pendingTransport=signal;transportStep=0;transportStarted=now;transportNext=now.AddMilliseconds(500);
        PauseFollowForTravel();if(MatchingTravelMenu(signal))return;RetryTravelInteraction(signal,now);
    }
}
