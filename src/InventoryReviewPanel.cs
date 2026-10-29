using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Task<DiagnosticExportResult>? inventoryReviewTask;
    private string inventoryReviewNotice="";
    private void PollInventoryReviewExport()
    {
        if(inventoryReviewTask?.IsCompleted!=true)return;
        var result=inventoryReviewTask.GetAwaiter().GetResult();inventoryReviewTask=null;
        inventoryReviewNotice=result.Error??("Inventory export saved: "+result.Path);
        PrintEquinox(inventoryReviewNotice);
    }
    private void DrawInventoryReviewExport()
    {
        ImGui.BeginDisabled(inventoryReviewTask!=null);
        if(ImGui.Button("Export inventories (.json)"))RetrievalTry(StartInventoryReviewExport);
        ImGui.EndDisabled();
        MarketHelp("All recorded characters and storage: retainers, bags, saddlebags, armoury, armoire, glamour dresser, FC chests and housing storage. Includes assignments and timestamps. Does not visit retainers or move items. Open storage in game to refresh missing/old records first.");
        if(ImGui.SmallButton("Open inventory exports"))OpenDiagnosticFolder(Path.Combine(Pi.GetPluginConfigDirectory(),"exports"));
        if(inventoryReviewTask!=null)ImGui.TextUnformatted("Writing inventory export…");
        else if(inventoryReviewNotice.Length>0)ImGui.TextWrapped(inventoryReviewNotice);
    }
    private void StartInventoryReviewExport()
    {
        if(inventoryReviewTask!=null)return;
        if(retrievalCatalog==null)throw new InvalidOperationException("Wait for the item catalogue to load.");
        var observations=config.Discoveries.ToList();
        var now=DateTimeOffset.UtcNow;var current=Player.IsLoaded?ReadActor():null;
        var live=Player.IsLoaded?ReadItemTradeBags():[];
        if(current!=null)foreach(var container in live.GroupBy(s=>s.Container)){
            var stacks=container.Where(s=>s.Item>0&&s.Quantity>0).GroupBy(s=>s.Key).Select(g=>new StorageStack(g.Key.Item,g.Sum(s=>s.Quantity),g.Key.Hq,retrievalCatalog.Names.GetValueOrDefault(g.Key.Item,g.Key.Item.ToString()),g.Select(s=>new StoragePosition(s.Slot+1,s.Quantity)).ToArray())).ToArray();
            observations.Add(new(Guid.NewGuid().ToString("N"),"storage.observed",now,current,null,Storage:new("bag:"+container.Key,container.Key==2001?"Crystals":"Inventory "+(container.Key+1),stacks.Select(s=>s.ItemId).Distinct().ToArray(),stacks)));
        }
        var recorded=config.OrganizerInventories.Select(r=>r with{Slots=r.Slots.ToArray()}).ToArray();
        var known=retrievalKnownRetainers.ToDictionary(x=>x.Key,x=>new Dictionary<ulong,string>(x.Value));
        foreach(var row in marketLedger.Retainers){if(!known.TryGetValue(row.CharacterId,out var list))known[row.CharacterId]=list=[];list[row.Id]=row.Name;}
        if(Player.IsLoaded){if(!known.TryGetValue(Player.ContentId,out var list))known[Player.ContentId]=list=[];foreach(var row in ReadSellerRetainers())list[row.Id]=row.Name;}
        var retainerNames=known.Values.SelectMany(x=>x).GroupBy(x=>x.Key).ToDictionary(g=>g.Key,g=>g.First().Value);
        var names=retrievalCatalog.Names;var categoryGroups=retrievalCatalog.Categories;
        var categoryNames=DataManager.GetExcelSheet<Lumina.Excel.Sheets.ItemUICategory>().ToDictionary(x=>x.RowId,x=>x.Name.ToString());
        var assignments=config.RetainerAssignments.ToDictionary(x=>x.Key,x=>x.Value.Select(a=>a with{Categories=a.Categories.ToArray(),Items=a.Items.ToArray(),Plans=a.Plans?.ToArray()}).ToArray());
        var useAssignments=config.UseRetainerAssignments.ToArray();
        var plans=config.RetrievalPresets.Select(p=>new {p.PlanId,p.Name,Items=p.Items.ToArray(),p.FilterPlanCategories,Categories=p.SendCategories?.ToArray()}).ToArray();
        var version=typeof(Plugin).Assembly.GetName().Version?.ToString();
        var path=Path.Combine(Pi.GetPluginConfigDirectory(),"exports","equinox-inventory-"+now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".json");
        inventoryReviewNotice="";
        inventoryReviewTask=Task.Run(async()=>{
            var temporary=path+".tmp";
            try{
                var categories=categoryGroups.SelectMany(g=>g.Value.Select(i=>(i.ItemId,Category:g.Key))).GroupBy(x=>x.ItemId).ToDictionary(g=>g.Key,g=>g.First().Category);
                var containers=InventoryReview.Build(observations,recorded,retainerNames,names,categories,categoryNames);
                var retained=containers.Where(c=>c.Key.StartsWith("retainer:")).GroupBy(c=>(c.CharacterId,Id:c.Key.Split(':')[1])).ToDictionary(g=>g.Key,g=>g.ToArray());
                var allRetainers=known.SelectMany(c=>c.Value.Select(r=>(CharacterId:c.Key.ToString(),Id:r.Key.ToString(),Name:r.Value)))
                    .Concat(retained.Select(g=>(g.Key.CharacterId,g.Key.Id,Name:retainerNames.GetValueOrDefault(ulong.Parse(g.Key.Id),g.Value.First().Name.Split(" · ")[0])))).DistinctBy(x=>(x.CharacterId,x.Id));
                var retainerSummary=allRetainers.Select(r=>{
                    var rows=retained.GetValueOrDefault((r.CharacterId,r.Id))??[];
                    var storage=rows.Where(c=>uint.TryParse(c.Key.Split(':').Last(),out var t)&&t is >=10000 and <=10006 or 12001).ToArray();
                    var normal=storage.Where(c=>!c.Key.EndsWith(":12001")).ToArray();
                    var occupied=normal.Length==7&&normal.All(c=>c.Items.All(i=>i.StackCount.HasValue))?(int?)normal.Sum(c=>c.Items.Sum(i=>i.StackCount!.Value)):null;
                    return new {r.CharacterId,r.Id,r.Name,RecordedContainers=storage.Length,ExpectedContainers=8,OccupiedSlots=occupied,CapacitySlots=175,FreeSlots=occupied.HasValue?175-occupied:null,
                        OldestObservation=storage.Length>0?(DateTimeOffset?)storage.Min(c=>c.ObservedAt):null,
                        Categories=storage.SelectMany(c=>c.Items).GroupBy(i=>new{i.CategoryId,i.Category}).Select(g=>new{g.Key.CategoryId,g.Key.Category,DistinctItems=g.Select(i=>i.ItemId).Distinct().Count(),Quantity=g.All(i=>i.Quantity.HasValue)?(long?)g.Sum(i=>(long)i.Quantity!.Value):null}).ToArray()};
                }).ToArray();
                var report=new {SchemaVersion=1,ExportedAt=now,PluginVersion=version,CurrentCharacter=current,
                    Notes=new[]{"Latest recorded snapshots from this Companion installation; not a live scan of every character or storage.","Current character bags/crystals refreshed at export. Open other storage in game to update its records.","Missing containers are unknown, not empty. Armoire/dresser presence-only records have null quantities and quality.","FC and housing storage are shared locations: only the latest observation per location is included; Character identifies its last observer.","Retainer summary covers seven item pages plus crystals. Equipped gear and market listings remain separate containers.","LiveBagCopies reports actual copy tradability; historical storage does not reliably record binding."},
                    Retainers=retainerSummary,Containers=containers,Assignments=assignments,CharactersUsingAssignments=useAssignments,SavedPlanGroups=plans,
                    LiveBagCopies=live.Where(s=>s.Item>0&&s.Quantity>0).ToArray()};
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))await JsonSerializer.SerializeAsync(file,report,new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
                File.Move(temporary,path);return new DiagnosticExportResult(path,null);
            }catch(Exception e){try{File.Delete(temporary);}catch{}return new DiagnosticExportResult(null,"Inventory export failed: "+e.Message);}
        });
    }
}
