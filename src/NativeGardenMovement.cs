using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed class NativeGardenMove
    {
        public required Queue<Vector3> Points;public required string Owner;public required DateTimeOffset Deadline;
        public required ulong Character;public required uint World,Territory;public ulong Target;public SharedGardenPlan? Plot;public OutdoorPlotEntry? Entry;
    }
    private NativeGardenMove? nativeGardenMove;
    private string nativeGardenMoveFailure="";
    private bool NativeGardenWalking=>nativeGardenMove!=null;
    private void StartNativeGardenMove(IEnumerable<Vector3> points,string owner,IGameObject? target=null,SharedGardenPlan? plot=null)
    {
        if(nativeGardenMove is {} prior&&prior.Owner!=owner)throw new InvalidOperationException("Another garden movement is active.");
        var route=points.ToArray();if(route.Length is <1 or >8||route.Any(p=>!NativeHousingPolicy.Position(p)))throw new InvalidOperationException("Garden route has invalid points.");
        if(LifestreamBusy()||nativeHousingTrip!=null||nativeWorldTrip!=null||nativeAethernetTrip!=null)throw new InvalidOperationException("Finish the active journey before garden walking.");
        StopIndependentWalk("Starting native garden movement",resetFailure:true);nativeGardenMoveFailure="";
        nativeGardenMove=new(){Points=new(route),Owner=owner,Deadline=DateTimeOffset.UtcNow.AddSeconds(45),Character=Player.ContentId,World=Player.CurrentWorld.RowId,Territory=Client.TerritoryType,Target=target?.GameObjectId??0,Plot=plot};
        RecordTravelPrompt("Garden movement started",new{owner,target=target?.BaseId,plot=plot?.Plot,ward=plot?.Ward,points=route.Select(FollowTravelPosition.From).ToArray()});
    }
    private void StopNativeGardenMove(string reason,string? owner=null)
    {
        if(nativeGardenMove is not {} move||owner!=null&&move.Owner!=owner)return;
        nativeGardenMove=null;StopIndependentWalk(reason);RecordTravelPrompt("Garden movement stopped",new{move.Owner,reason});
    }
    private void UpdateNativeGardenMove(DateTimeOffset now)
    {
        if(nativeGardenMove is not {} move)return;
        try{
            if(!Player.IsLoaded||Player.ContentId!=move.Character||Player.CurrentWorld.RowId!=move.World||Client.TerritoryType!=move.Territory)throw new InvalidOperationException("Garden walking stopped: character, world or territory changed.");
            if(move.Plot is {} destination&&atOutdoorEstate){
                if(SharedGardenLocation.Match(currentAddress,[destination])!=destination.HouseId)
                    throw new InvalidOperationException("Plot entry reached a different estate; garden movement stopped.");
                if(Objects.LocalPlayer is not {} entering)return;
                move.Entry??=new(new Vector3(MathF.Sin(entering.Rotation),0,MathF.Cos(entering.Rotation)));
                if(move.Entry.Confirm(entering.Position,true)){
                    RecordTravelPrompt("Garden plot entry confirmed",new{destination.HouseId,destination.Plot,destination.Ward,position=FollowTravelPosition.From(entering.Position)});
                    StopNativeGardenMove("Plot entry segment complete · ready to approach the garden");return;
                }
            }
            if(move.Plot!=null){
                if(!NativeTravelMovementReady())return;
                if(now>move.Deadline)throw new InvalidOperationException("Plot entry timed out before estate confirmation.");
                if(Objects.LocalPlayer is not {} player)return;
                move.Entry??=new(new Vector3(MathF.Sin(player.Rotation),0,MathF.Cos(player.Rotation)));
                WalkOutdoorPlotEntry(move.Entry,now);return;
            }
            if(now>move.Deadline)throw new InvalidOperationException("Native garden approach timed out.");
            var target=move.Target==0?null:Objects.FirstOrDefault(x=>x.GameObjectId==move.Target);
            if(move.Target!=0&&target==null)throw new InvalidOperationException("The mapped garden target is no longer loaded.");
            if(!NativeTravelMovementReady())return;
            if(!IndependentWalk(move.Points.Peek(),move.Plot!=null?2f:.3f,now,allowFlight:false,directDistance:8,interactionTarget:move.Points.Count==1?target:null,geometryOnly:true))return;
            move.Points.Dequeue();StopIndependentWalk("Garden route segment reached");
            if(move.Points.Count==0)StopNativeGardenMove("Native garden approach complete");
        }catch(Exception ex){nativeGardenMoveFailure=ex.Message;StopNativeGardenMove(ex.Message);}
    }
    private Vector3? NativeGardenInteriorLanding(uint territory,int plot)
    {
        var district=NativeHousingPolicy.District(DistrictName(territory)??"");
        if(district==null||plot is <1 or >60)throw new InvalidOperationException("Garden plot address is not supported.");
        var geometry=LoadHousingGeometry(district);if(!geometry.IsCompleted)return null;
        if(!geometry.IsCompletedSuccessfully)throw new InvalidOperationException(geometry.Exception?.GetBaseException().Message??"Housing layout unavailable.");
        // RoomExit is an interior landing anchor, not a verified street gate.
        // Geometry finds a supported route through the opening; plot identity
        // confirms entry and ends this leg before any garden target is chosen.
        return geometry.Result.InteriorLandings[plot-1]??throw new InvalidOperationException("This plot's entry destination is not recorded in the game layout. Export diagnostics; no Lifestream movement was started.");
    }
}
