using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private FollowPortalSignal? pendingWard;
    private DateTimeOffset wardAt,wardNext;
    private int wardStage;
    private unsafe void CaptureWardMenu(AtkUnitBase* addon,uint count,AtkValue* values)
    {
        if(!SharingTravel||usingSharedTravel||addon==null||addon!=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectBlock").Address||count!=2||values==null)return;
        if(((int)values[0].Type&15) is not (3 or 5)||values[0].Int!=1||((int)values[1].Type&15) is not (3 or 5)||values[1].Int is <0 or >=30)return;
        if(outgoingTravel==null&&Objects.LocalPlayer is {} self){var crystal=Objects.Where(x=>x.ObjectKind==ObjectKind.Aetheryte&&Vector3.Distance(x.Position,self.Position)<=x.HitboxRadius+4).MinBy(x=>Vector3.DistanceSquared(x.Position,self.Position));if(crystal!=null&&TravelSignal("ward",0,"",crystal.BaseId,crystal.Position) is {} signal)CaptureTravel(signal with {Ward=values[1].Int+1},0);}
        if(outgoingTravel is {TravelKind:"ward"} prior)outgoingTravel=prior with {Ward=values[1].Int+1};
    }
    private unsafe void UpdateFollowWard(DateTimeOffset now)
    {
        var sourceBlock=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectBlock").Address;
        if(SharingTravel&&!usingSharedTravel&&pendingWard==null&&sourceBlock!=null&&sourceBlock->IsVisible&&outgoingTravel==null&&Objects.LocalPlayer is {} self){
            var crystal=Objects.Where(x=>x.ObjectKind==ObjectKind.Aetheryte&&Vector3.Distance(x.Position,self.Position)<=x.HitboxRadius+4).MinBy(x=>Vector3.DistanceSquared(x.Position,self.Position));
            if(TravelSignal("ward",0,"",crystal?.BaseId??0,crystal?.Position??self.Position) is {} signal)CaptureTravel(signal with {Ward=1,SourceKind=crystal==null?"boundary":"Aetheryte"},0);
        }
        if(outgoingTravel is {TravelKind:"ward"} wardCandidate){
            var confirmation=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
            if(confirmation!=null&&confirmation->IsVisible&&confirmation->PromptText!=null){var text=confirmation->PromptText->NodeText.ToString();if(FollowPortalPolicy.IsConfirmationSupported(text))outgoingTravel=wardCandidate with {Confirmation=text};}
        }
        if(pendingWard is not {} s)return;
        if(!followSession.Armed||!config.EnableFollowThem||!config.FollowThem.UseSharedTeleports||now-wardAt>TimeSpan.FromSeconds(30)||Client.TerritoryType!=s.Territory||Player.CurrentWorld.RowId!=s.CurrentWorld||Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]||!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,s.Name,s.HomeWorld)){pendingWard=null;return;}
        if(!travelStepReady)return;
        if(now<wardNext)return;wardNext=now.AddMilliseconds(500);
        var yes=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        if(wardStage==3&&yes!=null&&yes->IsVisible&&yes->PromptText!=null){
            if(yes->PromptText->NodeText.ToString()==s.Confirmation){
                pendingWard=null;usingSharedTravel=true;try{yes->FireCallbackInt(0);}finally{usingSharedTravel=false;}FollowChatNotice("TRAVEL — Requested the same residential ward "+s.Ward+".");
            }else{pendingWard=null;FollowChatNotice("TRAVEL — Ward confirmation differs; waiting without accepting.");}return;
        }
        var block=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectBlock").Address;
        if(block!=null&&block->IsVisible){
            if(wardStage<2){
                var args=stackalloc AtkValue[2];args[0].Type=AtkValueType.Int;args[0].Int=1;args[1].Type=AtkValueType.Int;args[1].Int=s.Ward-1;
                usingSharedTravel=true;try{block->FireCallback(2,args,true);}finally{usingSharedTravel=false;}wardStage=2;return;
            }
            if(wardStage==2){
                var button=block->GetComponentButtonById(34);if(button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null)return;
                var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;
                for(var i=0;evt!=null&&i<16;i++,evt=evt->NextEvent)if(evt->State.EventType==AtkEventType.ButtonClick&&evt->Listener==(AtkEventListener*)block){wardStage=3;block->ReceiveEvent(evt->State.EventType,(int)evt->Param,evt);return;}
            }return;
        }
        var menu=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address;if(menu==null||!menu->IsVisible||menu->AtkValues==null||menu->AtkValuesCount<8||((int)menu->AtkValues[5].Type&15) is not (3 or 5))return;
        var count=menu->AtkValues[5].UInt;if(count>16||7+count>menu->AtkValuesCount)return;
        var go=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(6349).Text.ToString().Trim();
        for(var i=0;i<count;i++){var v=menu->AtkValues[7+i];if(((int)v.Type&15) is not (8 or 10))continue;var text=CopyMenuText(v.String.Value)?.Trim();
            if(wardStage==0&&text is "Residential District Aethernet." or "Residential District Aethernet"){menu->FireCallbackInt(i);wardStage=1;return;}
            if(wardStage<=1&&text==go){menu->FireCallbackInt(i);wardStage=1;return;}
        }
    }
}
