using System.Numerics;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool independentTravelOwned;
    private uint independentTravelDestination,independentTravelSourceTerritory;
    private Vector3 independentTravelSourcePosition;
    private DateTimeOffset independentTravelAt;
    private unsafe bool IndependentSkip(JsonElement step,string group)=>Objects.LocalPlayer is {} player&&IndependentSkipPolicy.Matches(step,group,Client.TerritoryType,player.Position,IndependentWork(),id=>QuestManager.Instance()!=null&&QuestManager.Instance()->IsQuestAccepted(id),id=>QuestManager.IsQuestComplete(id));
    private void CancelIndependentQuestTravel(){
        if(independentTravelOwned){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}}
        independentTravelOwned=false;independentTravelDestination=0;
    }
    private unsafe bool RequestIndependentTeleport(uint destination,DateTimeOffset now)
    {
        if(destination==0){StopIndependentQuest("No unlocked teleport or aethernet connection found for this destination.",true);return false;}
        var telepo=Telepo.Instance();if(telepo==null)return false;telepo->UpdateAetheryteList();
        foreach(var entry in telepo->TeleportList)if(entry.AetheryteId==destination){
            StopIndependentWalk();
            if(!telepo->Teleport(destination,entry.SubIndex))throw new InvalidOperationException("Teleport was refused.");
            independentTravelSent=true;independentTravelDestination=destination;independentTravelAt=now;return false;
        }
        StopIndependentQuest("Required aetheryte is not unlocked.",true);return false;
    }
    private unsafe bool IndependentQuestTravel(JsonElement step,uint territory,DateTimeOffset now)
    {
        var sheet=DataManager.GetExcelSheet<Aetheryte>();
        if(independentTravelOwned){
            questToolsStatus+=" · waiting for aethernet arrival";
            if(now-independentTravelAt>TimeSpan.FromSeconds(90))throw new InvalidOperationException("Aethernet arrival timed out.");
            if(LifestreamBusy()||now-independentTravelAt<TimeSpan.FromSeconds(3))return false;
            var expected=sheet.GetRowOrDefault(independentTravelDestination)?.Territory.RowId??0;
            if(expected!=0&&Client.TerritoryType!=expected)return false;
            if(Client.TerritoryType==independentTravelSourceTerritory&&Objects.LocalPlayer is {} arriving&&Vector3.Distance(arriving.Position,independentTravelSourcePosition)<8){questToolsStatus+=" · waiting for destination position";return false;}
            independentTravelOwned=false;independentTravelDestination=0;independentAethernetSent=true;
            return true; // Some imported None steps name the source territory.
        }
        if(independentTravelDestination!=0){
            var expected=sheet.GetRowOrDefault(independentTravelDestination)?.Territory.RowId??0;
            if(now-independentTravelAt<TimeSpan.FromSeconds(3)||Client.TerritoryType!=expected){
                if(now-independentTravelAt>TimeSpan.FromSeconds(90))throw new InvalidOperationException("Teleport arrival not confirmed.");return false;
            }
            independentTravelDestination=0;
        }
        var hasNet=step.TryGetProperty("AethernetShortcut",out var route)&&route.ValueKind==JsonValueKind.Array&&route.GetArrayLength()==2;
        var netId=hasNet?independentBook!.TravelId(route[1].GetString()??""):0;
        var sourceId=hasNet?independentBook!.TravelId(route[0].GetString()??""):0;
        var source=sheet.GetRowOrDefault(sourceId);
        if(hasNet&&Client.TerritoryType==territory&&step.TryGetProperty("Position",out var near)){var point=new Vector3(near.GetProperty("X").GetSingle(),near.GetProperty("Y").GetSingle(),near.GetProperty("Z").GetSingle());if(Objects.LocalPlayer is {} local&&Vector3.Distance(local.Position,point)<15)independentAethernetSent=true;}
        var netNeeded=hasNet&&!independentAethernetSent&&!IndependentSkip(step,"AethernetShortcutIf");
        var self=Objects.LocalPlayer;if(self==null)return false;
        // Process aethernet before demanding the final territory: city shards can cross zones.
        if(netNeeded){
            if(netId==0||source==null){StopIndependentQuest("Imported aethernet destination is not mapped.",true);return false;}
            var group=source.Value.AethernetGroup;
            var crystal=Objects.Where(o=>o.ObjectKind==Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte&&o.IsTargetable)
                .Where(o=>sheet.GetRowOrDefault(o.BaseId) is {} row&&row.AethernetGroup==group&&group!=0)
                .OrderBy(o=>Vector3.DistanceSquared(o.Position,self.Position)).FirstOrDefault();
            if(crystal!=null&&Vector3.Distance(crystal.Position,self.Position)<100){
                if(Vector3.Distance(crystal.Position,self.Position)>8){
                    var offset=self.Position-crystal.Position;offset.Y=0;
                    if(offset.LengthSquared()<.01f)throw new InvalidOperationException("Aethernet crystal is on another floor.");
                    var approach=crystal.Position+Vector3.Normalize(offset)*4;
                    Vector3? floor=(independentSearch!=null||independentPath!=null)&&Vector3.Distance(independentGoal,crystal.Position)<8?independentGoal:null;
                    for(var drop=0;floor==null&&drop<=4;drop+=2)if(independentCollision.Ground(approach-new Vector3(0,drop,0),out var ground)){floor=ground;break;}
                    if(floor==null)throw new InvalidOperationException("No supported approach beside the aethernet crystal.");
                    if(!IndependentWalk(floor.Value,.75f,now)){questToolsStatus+=" · approaching aethernet · "+independentWalkStatus;return false;}
                    if(Vector3.Distance(crystal.Position,self.Position)>8)throw new InvalidOperationException("Aethernet approach arrived outside interaction range.");
                }
                if(LifestreamBusy())throw new InvalidOperationException("Another travel operation is running.");
                StopIndependentWalk();
                if(!Pi.GetIpcSubscriber<uint,bool>("Lifestream.AethernetTeleportById").InvokeFunc(netId))throw new InvalidOperationException("Aethernet request was refused.");
                independentTravelSourceTerritory=Client.TerritoryType;independentTravelSourcePosition=self.Position;
                independentTravelOwned=true;independentTravelDestination=netId;independentTravelAt=now;return false;
            }
            var main=sheet.FirstOrDefault(a=>a.IsAetheryte&&a.AethernetGroup==group&&group!=0).RowId;
            if(independentTravelSent){StopIndependentQuest("No matching aethernet crystal loaded after teleport arrival.",true);return false;}
            return RequestIndependentTeleport(main,now);
        }
        if(independentAethernetSent&&IndependentStepPolicy.Text(step,"InteractionType")=="None")return true;
        var destination=independentBook!.TravelId(IndependentStepPolicy.Text(step,"AetheryteShortcut"));
        var far=step.TryGetProperty("Position",out var p)&&Vector3.Distance(self.Position,new(p.GetProperty("X").GetSingle(),p.GetProperty("Y").GetSingle(),p.GetProperty("Z").GetSingle()))>120;
        if(territory!=0&&Client.TerritoryType!=territory||destination!=0&&far&&!independentTravelSent&&!IndependentSkip(step,"AetheryteShortcutIf")){
            if(independentTravelSent){StopIndependentQuest("Travel connection did not reach the required territory.",true);return false;}
            if(destination==0)destination=sheet.FirstOrDefault(a=>a.IsAetheryte&&a.Territory.RowId==territory).RowId;
            // City subzones have no main crystal: recover a connection from the same quest's data.
            if(destination==0&&independentRoute!=null){
                foreach(var seq in independentRoute.Definition.GetProperty("QuestSequence").EnumerateArray()){
                    if(!seq.TryGetProperty("Steps",out var list))continue;
                    foreach(var candidate in list.EnumerateArray())if(IndependentStepPolicy.Number(candidate,"TerritoryId")==territory&&candidate.TryGetProperty("AethernetShortcut",out _))
                        return IndependentQuestTravel(candidate,territory,now);
                }
            }
            return RequestIndependentTeleport(destination,now);
        }
        return true;
    }
}
