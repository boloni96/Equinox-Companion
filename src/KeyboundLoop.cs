using System.Numerics;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using EquinoxCompanion.Keybound;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool keyboundQueueOwned,keyboundRegistrarOwned,keyboundAntiAfk=true;
    private DateTimeOffset keyboundLoopNext;
    private static unsafe AtkUnitBase* KeyboundAddon(string name){
        var stage=AtkStage.Instance();if(stage==null)return null;
        var addon=stage->RaptureAtkUnitManager->GetAddonByName(name);
        return addon!=null&&addon->IsVisible&&addon->IsReady?addon:null;
    }
    private static unsafe void KeyboundCallback(AtkUnitBase* addon,int command){
        var value=new AtkValue{Type=AtkValueType.Int,Int=command};
        addon->FireCallback(1,&value,true);
    }
    private unsafe bool UpdateKeyboundLoop(DateTimeOffset now)
    {
        if(!KeyboundPolicy.LoopTerritory(Client.TerritoryType)){keyboundStatus="Waiting: enter Keybound Square near the registrar";keyboundNext=now.AddMilliseconds(500);return true;}
        if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){keyboundStatus="Waiting: loading";return true;}
        var result=KeyboundAddon("KTGResult");
        if(Client.TerritoryType==1359&&result==null)return false;
        if(now<keyboundLoopNext)return true;
        keyboundLoopNext=now.AddSeconds(1);
        using var process=Process.GetCurrentProcess();
        if(process.MainWindowHandle==0||GetForegroundWindow()!=process.MainWindowHandle){keyboundStatus="Paused: bring this game window to the foreground";return true;}
        if(result!=null&&Client.TerritoryType==1359){
            var button=result->GetComponentButtonById(48);
            if(button!=null&&button->IsEnabled){if(!ClickLoginMenuButton("KTGResult",48))return true;keyboundLoopNext=now.AddSeconds(3);keyboundStatus="Returning to Keybound Square";}
            return true;
        }
        // Discard the old round's buffers before the next registration.
        keyboundSeen=keyboundAwaiting=keyboundRecalling=keyboundClearing=false;
        keyboundWord=keyboundSent=keyboundExpected=keyboundPendingSubmission=keyboundLastSubmitted="";
        keyboundTarget=0;keyboundSubmitAt=default;keyboundRecent.Clear();
        var confirm=KeyboundAddon("ContentsFinderConfirm");
        if(confirm!=null){
            if(KeyboundUi.Texts("ContentsFinderConfirm").Any(t=>t.Text.Contains("Keybound Brawler",StringComparison.OrdinalIgnoreCase))){
                KeyboundCallback(confirm,8);keyboundStatus="Accepting Keybound Brawler match";keyboundLoopNext=now.AddSeconds(3);
            }else keyboundStatus="Waiting: another duty confirmation is open";
            return true;
        }
        if(Conditions[ConditionFlag.InDutyQueue]||Conditions[ConditionFlag.WaitingForDuty]||Conditions[ConditionFlag.WaitingForDutyFinder]||Conditions[ConditionFlag.WaitingForRaceOrMatch]||Conditions[ConditionFlag.RegisteringForRaceOrMatch]){
            keyboundStatus="Queued: waiting for Keybound Brawler";return true;
        }
        var registration=KeyboundAddon("KTGEnterDialogue");
        if(registration!=null){
            var button=registration->GetComponentButtonById(9);
            if(button!=null&&button->IsEnabled){if(!ClickLoginMenuButton("KTGEnterDialogue",9))return true;keyboundQueueOwned=true;keyboundRegistrarOwned=false;keyboundLoopNext=now.AddSeconds(5);keyboundStatus="Registering for Keybound Brawler";}
            return true;
        }
        var talk=KeyboundAddon("Talk");
        if(talk!=null){
            if(keyboundRegistrarOwned&&Targets.Target?.BaseId==1058751){KeyboundCallback(talk,0);keyboundStatus="Speaking to Keybound registrar";}
            else keyboundStatus="Waiting: other conversation open";
            return true;
        }
        if(Conditions[ConditionFlag.OccupiedInEvent]){keyboundStatus="Waiting: event interaction";return true;}
        var player=Objects.LocalPlayer;if(player==null)return true;
        var registrar=Objects.Where(o=>o.BaseId==1058751&&Vector3.Distance(o.Position,player.Position)<=7).OrderBy(o=>Vector3.DistanceSquared(o.Position,player.Position)).FirstOrDefault();
        if(registrar==null){keyboundStatus="Waiting: move within 7 yalms of the Keybound registrar";return true;}
        var target=TargetSystem.Instance();if(target==null)return true;
        Targets.Target=registrar;keyboundRegistrarOwned=true;keyboundQueueOwned=false;
        target->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)registrar.Address,false);
        keyboundLoopNext=now.AddSeconds(2);keyboundStatus="Opening Keybound registration";
        return true;
    }
}
