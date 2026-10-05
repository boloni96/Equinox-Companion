using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Dictionary<string,JsonElement>? gardenPictures;
    private readonly Dictionary<string,string> gardenSupplies=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,(string Crop,double Days,double? Wilt)> gardenTiming=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,DateTimeOffset> completedGardenPlans=[];
    private readonly GardenProjectionCache gardenProjection = new();
    private SharedRoster? projectedGardenRoster;
    private (GardenListStamp Planting, GardenListStamp Tending, GardenListStamp Discoveries, GardenListStamp Mappings) projectedGardenInputs;
    private SyncEvent[] localGardenActions = [];
    private SharedGardenPlan[]? gardenPlanSources;
    private GardenListStamp acknowledgedGardenInputs;
    private HashSet<string> acknowledgedGardenEvents = [];
    private readonly Dictionary<(string House, int Batch), HashSet<int?>> queuedGardenBeds = [];

    private void RefreshGardenInputs()
    {
        var inputs = (GardenListStamp.Of(config.Planting), GardenListStamp.Of(config.Tending), GardenListStamp.Of(config.Discoveries), GardenListStamp.Of(gardenBedSync?.Mappings));
        if (ReferenceEquals(projectedGardenRoster, config.SharedRoster) && projectedGardenInputs == inputs) return;
        projectedGardenRoster = config.SharedRoster;
        projectedGardenInputs = inputs;
        localGardenActions = BuildLocalGardenActions().ToArray();
        gardenProjection.SetActions(localGardenActions);
        gardenPlanSources = null;
        queuedGardenBeds.Clear();
    }
    private HashSet<int?> QueuedGardenBeds(SharedGardenPlan plan)
    {
        RefreshGardenInputs();
        var sent = GardenListStamp.Of(config.SentEvents);
        if (acknowledgedGardenInputs != sent)
        {
            acknowledgedGardenInputs = sent;
            acknowledgedGardenEvents = config.SentEvents.ToHashSet();
            queuedGardenBeds.Clear();
        }
        var key = (plan.HouseId, plan.Batch);
        if (!queuedGardenBeds.TryGetValue(key, out var beds))
            queuedGardenBeds[key] = beds = gardenProjection.ActionsFor(plan).Where(e => !acknowledgedGardenEvents.Contains(e.Id)).Select(e => e.Bed).ToHashSet();
        return beds;
    }
    private void LoadGardenPictures()
    {
        if(gardenPictures is not null)return;
        gardenPictures=new(StringComparer.OrdinalIgnoreCase);
        try {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art/catalog/crops.json")));
            using var times=JsonDocument.Parse(File.ReadAllText(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art/catalog/timings.json")));
            foreach(var t in times.RootElement.EnumerateArray())gardenTiming[t.GetProperty("seed").GetString()!]=(t.GetProperty("crop").GetString()!,t.GetProperty("days").GetDouble(),t.GetProperty("wilt").ValueKind==JsonValueKind.Number?t.GetProperty("wilt").GetDouble():null);
            using var supplies=JsonDocument.Parse(File.ReadAllText(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art/catalog/supplies.json")));
            foreach(var row in supplies.RootElement.EnumerateArray())gardenSupplies[row.GetProperty("label").GetString()!]=row.GetProperty("path").GetString()!;
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
    private static string GardenVisualState(SharedGardenBed b,bool planVisible,DateTimeOffset now) => EquinoxCompanion.GardenVisualState.For(b,planVisible,now);
    private static string GardenCareIcon(SharedGardenBed b, bool planVisible, bool wrongBed)
    {
        if(planVisible&&GardenPlantRequirement.ActionIcon(b) is {} action)return action;
        if(planVisible&&GardenPlantRequirement.SuppressTending(b))return "starter";
        var actual=GardenVisualState(b,false,DateTimeOffset.UtcNow);
        return actual switch {"wet"=>"growing","due"=>"tend","wilt-estimated" or "wilted"=>"wilting",_=>actual};
    }
    private static string GardenCareAsset(string icon) => icon switch {"starter"=>"assets/badges/starter.png","remove-starter" or "replant"=>"assets/badges/replant.png","warning"=>"assets/badges/different.png","unknown"=>"assets/badges/unknown.png","dead"=>"assets/badges/dead.png",_=>"assets/icons/"+icon+".png"};
    private static string GardenCareHint(string icon) => icon switch
    {
        "starter" => "Temporary starter · finish the other planting steps, then replace this crop. No tending reminder for this temporary plant.",
        "remove-starter" => "Remove the temporary starter, then follow the replant step.",
        "replant" => "Starter removed · plant the final crop and soil for the replant step.",
        "water" => "Recently tended · wet for 12h after recorded care.",
        "tend" => "Tending suggested · tend this bed in game. 12h is a suggested interval, not a cooldown.",
        "ready" => "Ready to harvest · harvest when the crop is no longer needed as a neighbour for the planting plan.",
        "keep-mature" => "Keep this mature crop as a neighbour while completing the planting plan.",
        "growing" => "Growing · maturity has not been confirmed. Follow the care times below.",
        "empty" => "Empty · follow the planting plan when you are ready to plant.",
        "unknown" => "Crop or bed state unknown · open this numbered bed in game to inspect it.",
        "check-maturity" => "Estimated ready · mature appearance is based on timing. Open this bed in game to confirm readiness.",
        "at-risk" => "At risk (estimated) · check the crop and tend it if still growing.",
        "wilting" => "Wilting (estimated) · inspect the crop in game and tend it if alive.",
        "dead" => "Dead · confirmed. Remove the dead crop before replanting.",
        "dead-estimated" => "Dead (estimated) · verify in game before removing the crop.",
        "warning" => "Planting warning · check the plan icon above for the required crop, soil and bed.",
        _ => "Check this bed in game for its current state."
    };
    // Keep artwork and its hover target two pixels above the caption backing at every scale.
    private static Vector2 GardenCarePosition(Vector2 at,float size) => at+new Vector2(88*size/128,size-Math.Max(7,size*8/128)-ImGui.GetTextLineHeight()-3-size/4);
    private void DrawGardenTile(SharedGardenBed b,bool planVisible,Vector2 at,float size,bool wrongBed=false,bool selected=false)
    {
        LoadGardenPictures();var now=DateTimeOffset.UtcNow;var state=GardenVisualState(b,planVisible,now);
        var crop=EquinoxCompanion.GardenVisualState.DisplayCrop(b,planVisible);
        var animate=config.SharedRoster?.GardenAnimateEffects??true;
        var cropArtwork=EquinoxCompanion.GardenVisualState.CropArtwork(b,planVisible,now,animate);
        GardenImage("assets/beds/soil-"+EquinoxCompanion.GardenVisualState.SoilArtwork(b,planVisible,now)+".png",at,new(size));
        var hasPicture=gardenPictures!.TryGetValue(crop,out var picture);
        if(hasPicture)GardenImage(picture.GetProperty(cropArtwork).GetString()!,at,new(size));
        var effect=state switch {"ready" or "keep-mature"=>"ready-sparkles","wilt-estimated" or "wilted" or "at-risk"=>"wilt-mist","dead" or "dead-estimated"=>"dead-shade","unknown"=>"unknown-shade","planned"=>"planned-veil",_=>null};
        // Droplets request tending. Recorded care keeps wet soil and the same growth sprite.
        if(EquinoxCompanion.GardenVisualState.NeedsWater(b,planVisible,now)&&(!animate||EquinoxCompanion.GardenVisualState.EffectVisible(now)))GardenImage("assets/effects/wet-droplets.png",at,new(size));
        if(animate&&effect is "wet-droplets" or "ready-sparkles" or "wilt-mist"&&!EquinoxCompanion.GardenVisualState.EffectVisible(now))effect=null;
        if(effect is not null){var shade=effect is "unknown-shade" or "planned-veil" or "dead-shade";GardenImage("assets/effects/"+effect+".png",shade?at-new Vector2(size*.06f):at,new(size*(shade?1.12f:1)));}
        GardenImage("assets/beds/frame-"+(config.SharedRoster?.GardenFrame=="simple"?"simple":"wood")+".png",at,new(size));
        var border=state switch {"wilt-estimated" or "wilted"=>"wilt","check-maturity"=>"unknown","empty" or "growing"=>null,_=>state};
        if(planVisible&&GardenPlantRequirement.SuppressTending(b)&&border is "due" or "wet" or "at-risk" or "wilt" or "unknown")border=null;
        if(border is not null)GardenImage("assets/borders/"+border+".png",at,new(size));
        // Plan meaning stays at the top right; actual care belongs to the lower target.
        var overlay=wrongBed||b.Status=="different"?"different":b.Crop.Length>0?(b.Status=="confirmed"?"matched":"planned"):null;
        if(overlay is not null)GardenImage("assets/overlays/"+overlay+".png",at,new(size));
        else {
            var infoAt=at+new Vector2(91,7)*size/128;var infoSize=new Vector2(size/4);
            ImGui.GetWindowDrawList().AddRectFilled(infoAt,infoAt+infoSize,0xee30261c,3);
            ImGui.GetWindowDrawList().AddRect(infoAt,infoAt+infoSize,0xffacbdc8,3);
            GardenImage("assets/icons/no-plan.png",infoAt+new Vector2(size/32),infoSize-new Vector2(size/16));
        }
        // Separate actual-care symbol; a planned crop never supplies the actual state.
        var icon=GardenCareIcon(b,planVisible,wrongBed);
        GardenImage(GardenCareAsset(icon),GardenCarePosition(at,size),new Vector2(size/4));
        if(b.Queued)GardenImage("assets/icons/queued.png",GardenCarePosition(at,size)+new Vector2(-25,12)*size/128,new Vector2(20)*size/128);
        if(planVisible&&state!="dead"){var marker=b.Status switch {"starter"=>"starter","replant"=>"replant","different"=>"different",_=>null};
            if(marker is not null)GardenPlanOutline(marker,at,size);}
        if(selected)GardenImage("assets/borders/selected.png",at+new Vector2(size/32),new(size*15/16));
    }
    // Plan outline is outside the care perimeter; selection is inset. Preserve both meanings.
    private void GardenPlanOutline(string name,Vector2 at,float size) => GardenImage("assets/borders/"+name+".png",at-new Vector2(size/32),new Vector2(size*17/16));
    private void DrawGardenIdentity(SharedGardenBed bed,bool planVisible,Vector2 at,float size)
    {
        GardenImage($"assets/badges/bed-{bed.Bed}.png",at,new(size));
        if(!planVisible||bed.Order<=0)return;
        var paired=bed.Order==1&&bed.ReplantOrder==9;
        if(paired||bed.ReplantOrder==0&&bed.Order<=9)
        {
            var width=(paired?34:24)*size/128;
            GardenImage("assets/badges/step-"+(paired?"1-plus-9":bed.Order.ToString())+".png",at+new Vector2((size-width)/2,size/32),new(width,24*size/128));
        }
        else
        {
            var label=bed.Order+(bed.ReplantOrder>0?"+"+bed.ReplantOrder:"");var dims=ImGui.CalcTextSize(label);var p=at+new Vector2((size-dims.X)/2,size/32);
            var draw=ImGui.GetWindowDrawList();draw.AddRectFilled(p-new Vector2(2),p+dims+new Vector2(2),0xdd201710,3);draw.AddText(p,0xffffffff,label);
        }
    }
    private void DrawGardenItem(string label,bool seed=true,string prefix="")
    {
        LoadGardenPictures();var name=label.Trim();string? path=null;
        if(gardenSupplies.TryGetValue(name,out var supply))path=supply;
        else if(gardenPictures!.TryGetValue(GardenCropName(name.EndsWith(" seed",StringComparison.OrdinalIgnoreCase)?name[..^5]:name),out var picture))path=picture.GetProperty(seed?"seedIcon":"produceIcon").GetString();
        if(path is not null){var texture=Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"garden-art",path)).GetWrapOrDefault();if(texture is not null){ImGui.Image(texture.Handle,new Vector2(ImGui.GetTextLineHeight()));ImGui.SameLine();}}
        ImGui.TextWrapped(prefix+label);
    }
    private IEnumerable<SyncEvent> BuildLocalGardenActions()
    {
        var mappings=GardenMappings().ToArray();
        return config.Planting.Select(p=>new SyncEvent(p.EventId,"garden.planted",p.ConfirmedAt,WithWorldNames(p.Actor),WithAddressNames(p.Address),p.Patch,p.Bed,p.Plant))
        .Concat(config.Tending.Select(t=>new SyncEvent(t.EventId,"garden.tended",t.ConfirmedAt,WithWorldNames(t.Actor),WithAddressNames(t.Address),t.Patch,t.Bed)))
        .Concat(config.Discoveries.Where(e=>e.Kind is "garden.empty" or "garden.empty.unmapped" or "garden.unmapped" or "garden.dead" or "garden.ready" or "garden.observed" or "garden.fertilized" or "garden.status" or "garden.status.unmapped")).Select(e=>GardenTargetMap.Resolve(e,mappings));
    }
    private IEnumerable<SharedGardenTarget> GardenMappings() => (config.SharedRoster?.GardenTargets??[])
        .Concat(config.Discoveries.Select(GardenTargetMap.FromNumbered).OfType<SharedGardenTarget>())
        .Concat((gardenBedSync?.Mappings??[]).Select(e=>GardenTargetMap.FromNumbered(e with {Address=WithAddressNames(e.Address!)})).OfType<SharedGardenTarget>());
    private void GardenActionRecorded(){if(syncFailures==0)nextSync=default;}
    private SharedGardenPlan EffectiveGardenPlan(SharedGardenPlan plan)
    {
        RefreshGardenInputs();
        return gardenProjection.Get(plan, ProjectGardenPlan);
    }
    private SharedGardenPlan ProjectGardenPlan(SharedGardenPlan plan, SyncEvent[] actions)
    {
        var key=plan.HouseId+":"+plan.Batch+":"+plan.At.ToUnixTimeMilliseconds();
        LoadGardenPictures();
        if(completedGardenPlans.TryGetValue(key,out var at))plan=plan with {CompletedAt=at};
        var result=GardenLive.Apply(plan,actions,GardenCropName,n=>gardenTiming.TryGetValue(n,out var t)?t.Days:0,n=>gardenTiming.TryGetValue(n,out var t)?t.Wilt:null);
        result=result with {Beds=result.Beds.Select(b=>b.DeadConfirmedAt is null&&b.Planted is {} planted&&b.HarvestAt is null&&gardenTiming.TryGetValue(b.ActualCrop,out var timing)?b with {Days=timing.Days,WiltHours=timing.Wilt,HarvestAt=timing.Days>0?planted.AddDays(timing.Days):null}:b).ToArray()};
        if(result.CompletedAt is {} completed){if(completedGardenPlans.Count>200)completedGardenPlans.Clear();completedGardenPlans[key]=completed;}
        return result;
    }
}
