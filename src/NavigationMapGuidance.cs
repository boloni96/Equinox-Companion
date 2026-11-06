using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Data.Files;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record MapGuidanceLoad(Task<NavigationRoadMap?> Task,DateTimeOffset Started,string Path);
    private readonly Dictionary<uint,MapGuidanceLoad> navigationMapLoads=[];
    private NavigationRoadMap? independentRoadGuide;
    private uint independentRoadMapId;
    private NavigationRoadRoute? independentRoadRoute;
    private Task<NavigationRoadRoute?>? navigationRoadRouteTask;
    private NavigationRoadMap? navigationRoadRouteMap;
    private System.Numerics.Vector3 navigationRoadRouteStart,navigationRoadRouteGoal;
    private DateTimeOffset navigationRoadRouteAt;
    private unsafe bool PrepareNavigationMap(DateTimeOffset now,System.Numerics.Vector3 start,System.Numerics.Vector3 goal)
    {
        var agent=AgentMap.Instance();var maps=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>();
        var id=agent==null?0:agent->CurrentMapId;var row=maps.GetRowOrDefault(id);
        if(row==null||row.Value.TerritoryType.RowId!=Client.TerritoryType){
            id=DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRow(Client.TerritoryType).Map.RowId;row=maps.GetRowOrDefault(id);
        }
        independentRoadGuide=null;independentRoadRoute=null;independentRoadMapId=id;
        if(row==null||row.Value.TerritoryType.RowId!=Client.TerritoryType||row.Value.SizeFactor==0)return true;
        if(!navigationMapLoads.TryGetValue(id,out var load)){
            if(navigationMapLoads.Count>=8)navigationMapLoads.Remove(navigationMapLoads.Keys.First());
            var name=row.Value.Id.ToString();if(string.IsNullOrWhiteSpace(name)||!name.Contains('/'))return true;
            var path="ui/map/"+name+"/"+name.Replace("/","")+"_m.tex";
            var size=row.Value.SizeFactor;var x=row.Value.OffsetX;var z=row.Value.OffsetY;
            load=new(Task.Run(()=>{
                var tex=DataManager.GetFile<TexFile>(path);if(tex==null)return null;
                var buffer=tex.TextureBuffer.Filter(0,0,TexFile.TextureFormat.B8G8R8A8);
                return NavigationRoadMap.FromRgba(buffer.RawData,buffer.Width,buffer.Height,size,x,z,bgra:true);
            }),now,path);navigationMapLoads[id]=load;
        }
        if(!load.Task.IsCompleted&&now-load.Started<TimeSpan.FromSeconds(2)){independentWalkStatus="Reading game map road guidance";return false;}
        if(load.Task.IsCompletedSuccessfully)independentRoadGuide=load.Task.Result;
        if(independentRoadGuide is {} guide){
            if(navigationRoadRouteTask==null||navigationRoadRouteMap!=guide||System.Numerics.Vector3.DistanceSquared(navigationRoadRouteGoal,goal)>1||System.Numerics.Vector3.DistanceSquared(navigationRoadRouteStart,start)>16){
                navigationRoadRouteMap=guide;navigationRoadRouteStart=start;navigationRoadRouteGoal=goal;navigationRoadRouteAt=now;
                navigationRoadRouteTask=Task.Run(()=>guide.BuildRoute(start,goal));
            }
            if(!navigationRoadRouteTask.IsCompleted&&now-navigationRoadRouteAt<TimeSpan.FromSeconds(2)){
                independentWalkStatus="Connecting game map roads";return false;
            }
            if(navigationRoadRouteTask.IsCompletedSuccessfully)independentRoadRoute=navigationRoadRouteTask.Result;
        }
        RecordTravelPrompt("Navigation map guidance",new{map=id,territory=Client.TerritoryType,asset=load.Path,available=independentRoadGuide!=null,connectedRoadGuide=independentRoadRoute!=null,roadCells=independentRoadRoute?.Cells,roadRoute=independentRoadRoute?.Points.Where((_,i)=>i%Math.Max(1,(int)Math.Ceiling(independentRoadRoute.Points.Length/256d))==0).Select(FollowTravelPosition.From).ToArray(),coverage=independentRoadGuide?.RoadCoverage,source="Game map/minimap coordinates; geometry remains authoritative",reason=load.Task.IsFaulted?load.Task.Exception?.GetBaseException().Message:null});
        return true;
    }
}
