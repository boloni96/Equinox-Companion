using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Dictionary<string,JsonElement>? gardenPictures;
    private readonly Dictionary<string,(string Crop,double Days,double? Wilt)> gardenTiming=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,DateTimeOffset> completedGardenPlans=[];
    private readonly Dictionary<string,SharedGardenPlan> localGardenCache=[];
    private SharedRoster? projectedGardenRoster;
    private string projectedGardenActions="";
    private void LoadGardenPictures()
    {
        if(gardenPictures is not null)return;
        gardenPictures=new(StringComparer.OrdinalIgnoreCase);
        try {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art/catalog/crops.json")));
            using var times=JsonDocument.Parse(File.ReadAllText(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art/catalog/timings.json")));
            foreach(var t in times.RootElement.EnumerateArray())gardenTiming[t.GetProperty("seed").GetString()!]=(t.GetProperty("crop").GetString()!,t.GetProperty("days").GetDouble(),t.GetProperty("wilt").ValueKind==JsonValueKind.Number?t.GetProperty("wilt").GetDouble():null);
            foreach(var row in document.RootElement.EnumerateArray()){
                var value=row.Clone();gardenPictures[value.GetProperty("cropLabel").GetString()!]=value;
                foreach(var alias in value.GetProperty("aliases").EnumerateArray())gardenPictures[alias.GetString()!]=value;
                gardenPictures[value.GetProperty("plantingMaterialLabel").GetString()!]=value;
            }
        } catch(Exception e){errorJournal.Record("garden-art","Artwork catalogue unavailable; text guide remains available.",exceptionType:e.GetType().Name);}
    }
    private string GardenCropName(string seed){LoadGardenPictures();return gardenTiming.TryGetValue(seed,out var t)?t.Crop:gardenPictures!.TryGetValue(seed,out var c)?c.GetProperty("cropLabel").GetString()!:seed;}
    private void GardenImage(string path,Vector2 at,Vector2 size)
    {
        var image=Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art",path)).GetWrapOrDefault();
        if(image is not null)ImGui.GetWindowDrawList().AddImage(image.Handle,at,at+size);
    }
    private void DrawGardenTile(SharedGardenBed b,bool planVisible,Vector2 at,float size)
    {
        LoadGardenPictures();var now=DateTimeOffset.UtcNow;
        var planned=planVisible&&b.Crop.Length>0;var crop=planned?b.Crop:b.ActualCrop;
        var ready=!planned&&b.Ready;var wet=(!planned||b.Status is "confirmed" or "starter")&&!b.Ready&&b.Watered is {} w&&w<=now&&now-w<TimeSpan.FromHours(12);
        var death=b.Watered is {} care&&b.WiltHours is {} wilt?care.AddHours(wilt+24):(DateTimeOffset?)null;
        var dead=!ready&&!planned&&GardenTiming.DeathRisk(b.Ready,death,b.HarvestAt,now);
        GardenImage("assets/beds/soil-"+(dead?"dead":wet?"wet":"normal")+".png",at,new(size));
        if(gardenPictures!.TryGetValue(crop,out var picture)){
            GardenImage(picture.GetProperty(ready?"plantMature":dead?"plantDead":"plantGrowing").GetString()!,at,new(size));
            if(wet&&(!planned||b.Status is "confirmed" or "starter"))GardenImage("assets/effects/wet-droplets.png",at,new(size));
        }else if(crop!="Empty")GardenImage("assets/overlays/unknown.png",at,new(size));
        if(planned&&b.Status is not ("confirmed" or "starter"))GardenImage("assets/effects/planned-veil.png",at,new(size));
        GardenImage("assets/beds/frame-wood.png",at,new(size));
        var border=planned?b.Status switch {"confirmed"=>"ready","starter"=>"starter","replant"=>"replant","different"=>"different",_=>"planned"}:ready?"ready":dead?"dead-estimated":wet?"wet":b.NextTend<=now?"due":"unknown";
        GardenImage("assets/borders/"+border+".png",at,new(size));
        if(planned&&b.Status=="different") ImGui.GetWindowDrawList().AddRect(at+new Vector2(2),at+new Vector2(size-2),0xff5555ff,2,ImDrawFlags.None,3);
    }
    private IEnumerable<SyncEvent> LocalGardenActions()=>config.Planting.Select(p=>new SyncEvent(p.EventId,"garden.planted",p.ConfirmedAt,WithWorldNames(p.Actor),WithAddressNames(p.Address),p.Patch,p.Bed,p.Plant))
        .Concat(config.Tending.Select(t=>new SyncEvent(t.EventId,"garden.tended",t.ConfirmedAt,WithWorldNames(t.Actor),WithAddressNames(t.Address),t.Patch,t.Bed)))
        .Concat(config.Discoveries.Where(e=>e.Kind is "garden.empty" or "garden.ready" or "garden.observed" or "garden.fertilized"));
    private void GardenActionRecorded(){if(syncFailures==0)nextSync=default;}
    private SharedGardenPlan EffectiveGardenPlan(SharedGardenPlan plan)
    {
        var actions=$"{config.Planting.LastOrDefault()?.EventId}:{config.Tending.LastOrDefault()?.EventId}:{config.Discoveries.LastOrDefault()?.Id}";
        if(projectedGardenRoster!=config.SharedRoster||projectedGardenActions!=actions){localGardenCache.Clear();projectedGardenRoster=config.SharedRoster;projectedGardenActions=actions;}
        var key=plan.HouseId+":"+plan.Batch+":"+plan.At.ToUnixTimeMilliseconds();
        if(localGardenCache.TryGetValue(key,out var cached))return cached;
        LoadGardenPictures();
        if(completedGardenPlans.TryGetValue(key,out var at))plan=plan with {CompletedAt=at};
        var result=GardenLive.Apply(plan,LocalGardenActions(),GardenCropName,n=>gardenTiming.TryGetValue(n,out var t)?t.Days:0,n=>gardenTiming.TryGetValue(n,out var t)?t.Wilt:null);
        result=result with {Beds=result.Beds.Select(b=>b.Planted is {} planted&&b.HarvestAt is null&&gardenTiming.TryGetValue(b.ActualCrop,out var timing)?b with {Days=timing.Days,WiltHours=timing.Wilt,HarvestAt=timing.Days>0?planted.AddDays(timing.Days):null}:b).ToArray()};
        if(result.CompletedAt is {} completed){if(completedGardenPlans.Count>200)completedGardenPlans.Clear();completedGardenPlans[key]=completed;}
        localGardenCache[key]=result;return result;
    }
}
