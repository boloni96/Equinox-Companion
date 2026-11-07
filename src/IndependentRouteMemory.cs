using System.Numerics;
using System.Text.Json;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private List<RememberedWalkingRoute>? independentSavedRoutes;
    private static readonly JsonSerializerOptions WalkingMemoryJson=new(){IncludeFields=true};
    private string WalkingMemoryPath=>Path.Combine(Pi.GetPluginConfigDirectory(),"walking-routes-v2.json");
    private void LoadIndependentRoutes(){
        if(independentSavedRoutes!=null)return;independentSavedRoutes=[];
        try{
            if(!File.Exists(WalkingMemoryPath)||new FileInfo(WalkingMemoryPath).Length>4000000)return;
            independentSavedRoutes=(JsonSerializer.Deserialize<List<RememberedWalkingRoute>>(File.ReadAllText(WalkingMemoryPath),WalkingMemoryJson)??[])
                .Where(r=>r.Points is {Length:>=2 and <=2048}&&r.Points.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))).TakeLast(256).ToList();
        }catch(Exception e){Log.Debug(e,"Saved walking routes unavailable");}
    }
    private Vector3[]? RecallIndependentRoute(Vector3 position,Vector3 goal,float range){
        LoadIndependentRoutes();var route=WalkingRouteMemory.Join(independentSavedRoutes!,Client.TerritoryType,position,goal,range,independentCollision.Traverse)??WalkingRouteNetwork.Find(independentSavedRoutes!,Client.TerritoryType,position,goal,range,independentCollision.Traverse);
        if(route!=null)TraceKeyboundRun(independentNavigationRun,"route-reused",$"{route.Length} saved points · live checks remain enabled","companion-navigation",false);
        if(route==null)RecordTravelPrompt("Navigation cache miss",new{run=independentNavigationRun,territory=Client.TerritoryType,
            savedRoutes=independentSavedRoutes!.Count,territoryRoutes=independentSavedRoutes.Count(r=>r.Territory==Client.TerritoryType),
            start=FollowTravelPosition.From(position),goal=FollowTravelPosition.From(goal),reason="No complete route with matching destination and a valid start/end connector"});
        return route;
    }
    private void SaveIndependentRoutes(){
        try{var temporary=WalkingMemoryPath+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(independentSavedRoutes,WalkingMemoryJson));File.Move(temporary,WalkingMemoryPath,true);}
        catch(Exception e){Log.Debug(e,"Could not save walking routes");}
    }
    private void RememberIndependentRoute(Vector3[] points){
        if(points.Length is <2 or >2048)return;LoadIndependentRoutes();
        independentSavedRoutes!.RemoveAll(r=>r.Territory==independentTerritory&&Vector3.Distance(r.Goal,independentGoal)<.1f&&Vector3.Distance(r.Points[0],points[0])<2);
        independentSavedRoutes.Add(new(independentTerritory,independentGoal,independentRange,points));
        if(independentSavedRoutes.Count>256)independentSavedRoutes.RemoveRange(0,independentSavedRoutes.Count-256);
        while(independentSavedRoutes.Count>0&&independentSavedRoutes.Sum(r=>r.Points.Length)>24000)independentSavedRoutes.RemoveAt(0);
        SaveIndependentRoutes();
    }
    private Vector3[]? RecallIndependentPrefix(Vector3 position,Vector3 goal){
        LoadIndependentRoutes();
        var path=WalkingRouteMemory.Prefix(independentSavedRoutes!,independentTerritory,position,goal,IndependentTraversableConnector);
        if(path!=null)RecordTravelPrompt("Navigation cached section",new{run=independentNavigationRun,points=path.Length,end=FollowTravelPosition.From(path[^1]),remaining=Vector3.Distance(path[^1],goal)});
        return path;
    }
    private void ForgetIndependentRoute(Vector3 blocked){
        LoadIndependentRoutes();
        var retained=new List<RememberedWalkingRoute>();
        foreach(var route in independentSavedRoutes!){
            if(route.Territory==independentTerritory&&route.Points.Any(p=>Vector3.Distance(p,blocked)<3))retained.AddRange(WalkingRouteMemory.Cut(route,blocked));
            else retained.Add(route);
        }
        // Preserve actual progress, ending before the failed approach. Never
        // persist a speculative planned path as a successfully travelled route.
        var travelled=independentWalkedPoints.Take(Math.Max(0,independentWalkedPoints.Count-2)).ToArray();
        if(travelled.Length>=2)retained.Add(new(independentTerritory,travelled[^1],.25f,travelled));
        independentSavedRoutes=retained.TakeLast(256).ToList();
        while(independentSavedRoutes.Count>0&&independentSavedRoutes.Sum(r=>r.Points.Length)>24000)independentSavedRoutes.RemoveAt(0);
        SaveIndependentRoutes();
        RecordTravelPrompt("Navigation cache section invalidated",new{run=independentNavigationRun,blocked=FollowTravelPosition.From(blocked),retainedRoutes=independentSavedRoutes.Count,observedPointsSaved=travelled.Length});
    }
}
