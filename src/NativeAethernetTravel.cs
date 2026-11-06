using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed class NativeAethernetTrip
    {
        public required uint Destination,World;public required ulong Character;public required DateTimeOffset Started;
        public DateTimeOffset Next,InteractionAt;public int Stage,Selections,ApproachAttempt;public bool Teleported;public Vector3 Source;public Vector3? ApproachGoal;
    }
    private NativeAethernetTrip? nativeAethernetTrip;
    private string nativeAethernetStatus="";private bool nativeAethernetSucceeded;
    private bool BeginNativeAethernet(uint id,out string reason)
    {
        if(NativeGardenWalking||nativeAethernetTrip!=null||nativeWorldTrip!=null||nativeHousingTrip!=null||LifestreamBusy()){reason="Another travel operation is active.";return false;}
        var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>().GetRowOrDefault(id);
        if(row==null||row.Value.AethernetGroup==0){reason="No supported aethernet connection for this destination.";return false;}
        nativeAethernetSucceeded=false;nativeAethernetTrip=new(){Destination=id,World=Player.CurrentWorld.RowId,Character=Player.ContentId,Started=DateTimeOffset.UtcNow};
        nativeAethernetStatus="Reaching the aethernet";reason="";RecordTravelPrompt("Aethernet requested",new{id});return true;
    }
    private void StopNativeAethernet(string reason,bool success=false){if(nativeAethernetTrip==null)return;nativeAethernetTrip=null;nativeAethernetSucceeded=success;StopIndependentWalk(reason);nativeAethernetStatus=reason;RecordTravelPrompt("Aethernet ended",new{reason});}
    private unsafe void UpdateNativeAethernet(DateTimeOffset now)
    {
        if(nativeAethernetTrip is not {} trip||now<trip.Next)return;trip.Next=now.AddMilliseconds(500);
        try{
            if(now-trip.Started>TimeSpan.FromMinutes(3))throw new InvalidOperationException("Aethernet timed out; export the travel prompt history.");
            if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
            if(Player.ContentId!=trip.Character||Player.CurrentWorld.RowId!=trip.World||Conditions[ConditionFlag.InCombat])throw new InvalidOperationException("Aethernet stopped: character, world or combat state changed.");
            var self=Objects.LocalPlayer;if(self==null)return;
            var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>();var destination=sheet.GetRow(trip.Destination);
            if(trip.Selections>0&&Client.TerritoryType==destination.Territory.RowId&&Vector3.Distance(self.Position,trip.Source)>8&&IndependentCrystalPosition(trip.Destination) is {} point&&Vector3.Distance(self.Position,point)<30){StopNativeAethernet("Aethernet arrival confirmed",true);return;}
            if(trip.Stage==0){
                if(Conditions[ConditionFlag.Casting]||!NativeTravelMovementReady())return;
                var crystal=Objects.Where(x=>x.ObjectKind==ObjectKind.Aetheryte&&x.IsTargetable&&sheet.GetRowOrDefault(x.BaseId)?.AethernetGroup==destination.AethernetGroup).MinBy(x=>Vector3.DistanceSquared(self.Position,x.Position));
                if(crystal==null||Vector3.Distance(self.Position,crystal.Position)>100){
                    if(trip.Teleported)return;
                    var main=sheet.FirstOrDefault(x=>x.IsAetheryte&&x.AethernetGroup==destination.AethernetGroup).RowId;
                    if(main==0||!RequestNativePublicTeleport(main))throw new InvalidOperationException("No available teleport to this aethernet group.");trip.Teleported=true;return;
                }
                if(!TargetWithinInteractionRange(crystal)){
                    var offset=self.Position-crystal.Position;offset.Y=0;if(offset.LengthSquared()<.01f)throw new InvalidOperationException("Aethernet crystal is on another floor.");
                    if(trip.ApproachGoal is not {} floor){if(!TryNativeTravelGround(NativeAetheryteApproach.InteractionEdge(self.Position,crystal.Position,InteractionLimit(crystal)),self.Position,crystal.Position,out floor)){nativeAethernetStatus="Waiting for walkable ground beside the aethernet";return;}trip.ApproachGoal=floor;}
                    IndependentWalk(floor,.65f,now,allowFlight:false,directDistance:8,interactionTarget:crystal);nativeAethernetStatus="Approaching aethernet · "+independentWalkStatus;return;
                }
                StopIndependentWalk("At aethernet");var system=TargetSystem.Instance();if(system==null)return;
                if(Targets.Target?.Address!=crystal.Address){Targets.Target=crystal;return;}
                RecordTravelPrompt("Aetheryte interaction",new{crystal=crystal.BaseId,distance=Vector3.Distance(self.Position,crystal.Position),trip.ApproachAttempt});
                trip.Source=self.Position;trip.Stage=1;trip.InteractionAt=now;system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)crystal.Address,false);return;
            }
            var menu=(AddonSelectString*)SellerAddon("SelectString");
            if(trip.Stage==1&&menu!=null){
                var label=DataManager.GetExcelSheet<NativeEventQuestText>(name:"transport/Aetheryte").GetRowOrDefault(1)?.Text??"";
                var choices=TransportChoices((AtkUnitBase*)menu);var index=choices.FindIndex(x=>NativeEventPolicy.Prompt(label,x));
                var list=menu->PopupMenu.PopupMenu.List;
                if(index>=0&&list!=null&&!list->GetItemDisabledState(index)){list->DispatchItemEvent(index,AtkEventType.ListItemClick);trip.Stage=2;return;}
            }
            var town=SellerAddon("TelepotTown");
            if(trip.Stage==1&&menu==null&&town==null&&now-trip.InteractionAt>=TimeSpan.FromSeconds(trip.ApproachAttempt==0?2:5)){
                if(trip.ApproachAttempt>=2)throw new InvalidOperationException("The aethernet menu did not open after the closer approach.");
                trip.ApproachAttempt++;trip.ApproachGoal=null;trip.Stage=0;nativeAethernetStatus="Aethernet did not respond; moving a little closer";return;
            }
            if(town==null||trip.Selections>=2)return;
            var agent=AgentTelepotTown.Instance();if(agent==null||agent->Data==null||agent->Data->AetheryteCount>64)return;
            var matches=AethernetChoices(town).Where(x=>x.Name.Trim()==destination.AethernetName.Value.Name.ToString().Trim()).ToArray();if(matches.Length!=1)return;
            var callback=matches[0].Callback;if(callback>=agent->Data->AetheryteCount)return;
            var entry=agent->Data->Entries[(int)callback];
            if(entry.AetheryteId!=trip.Destination||entry.IsLocked||entry.IsUnusable)throw new InvalidOperationException("The selected shard is unavailable or differs from the requested destination.");
            if(entry.IsCurrent&&trip.Selections==0){StopNativeAethernet("Already at the requested shard",true);return;}
            NativeTravelCallback(town,11,(int)callback);trip.Selections++;trip.Stage=3;trip.Next=now.AddSeconds(2);nativeAethernetStatus="Travelling to "+destination.AethernetName.Value.Name.ToString();
        }catch(Exception ex){StopNativeAethernet(ex.Message);}
    }
}
