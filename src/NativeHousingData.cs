using System.Numerics;
using Lumina.Data.Files;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record HousingShard(uint Id,uint Instance,string Name,byte Order,Vector3 Position);
    private sealed record HousingGeometry(Vector3[] Placards,HousingShard[] Shards);
    private readonly Dictionary<uint,Task<HousingGeometry>> nativeHousingGeometry=[];
    private Task<HousingGeometry> LoadHousingGeometry(NativeHousingDistrict district)
    {
        if(nativeHousingGeometry.TryGetValue(district.Territory,out var existing)&&!existing.IsFaulted)return existing;
        var land=DataManager.GetExcelSheet<Lumina.Excel.Sheets.HousingLandSet>().GetRow(district.LandSet);
        var plots=land.LandSet.Select(x=>x.PlacardId).ToArray();
        var bg=DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRow(district.Territory).Bg.ToString();
        var root="bg/"+bg[..(bg.LastIndexOf('/')+1)];
        var shards=DataManager.GetExcelSheet<Lumina.Excel.Sheets.HousingAethernet>().Where(x=>x.TerritoryType.RowId==district.Territory)
            .Select(x=>new HousingShard(x.RowId,x.Level.RowId,x.PlaceName.Value.Name.ToString(),x.Order,default)).ToArray();
        // File IO and layer parsing are kept out of Update/Draw. No game pointers on this worker.
        var task=Task.Run(()=>{
            var positions=new Dictionary<uint,Vector3>();
            foreach(var file in new[]{"planevent.lgb","planmap.lgb","planlive.lgb"}){
                var lgb=DataManager.GetFile<LgbFile>(root+file);if(lgb==null)continue;
                foreach(var layer in lgb.Layers)foreach(var obj in layer.InstanceObjects){
                    var p=obj.Transform.Translation;var point=new Vector3(p.X,p.Y,p.Z);
                    if(NativeHousingPolicy.Position(point))positions.TryAdd(obj.InstanceId,point);
                }
            }
            if(plots.Length<60||plots.Take(60).Any(id=>id==0||!positions.ContainsKey(id)))throw new InvalidOperationException($"Housing layout {district.Territory} at {root}: {positions.Count} objects; missing plot instances {string.Join(",",plots.Take(60).Where(id=>!positions.ContainsKey(id)).Take(8))}. Export the report; no route sent to another plugin.");
            var located=shards.Where(s=>positions.ContainsKey(s.Instance)).Select(s=>s with{Position=positions[s.Instance]}).OrderBy(s=>s.Order).ToArray();
            if(located.Length!=shards.Length||located.Length<2)throw new InvalidOperationException($"Housing layout {district.Territory}: found {located.Length}/{shards.Length} shards; missing {string.Join(",",shards.Where(s=>!positions.ContainsKey(s.Instance)).Select(s=>s.Instance))}.");
            return new HousingGeometry(plots.Take(60).Select(id=>positions[id]).ToArray(),located);
        });
        nativeHousingGeometry[district.Territory]=task;return task;
    }
}
