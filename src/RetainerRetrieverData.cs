using System.Text.Json;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record RetrievalCatalog(Dictionary<uint,string> Names,Dictionary<uint,uint> Icons,Dictionary<uint,RetrievalRecipe[]> Recipes,RetrievalPreset[] Workshop);
    private Task<RetrievalCatalog>? retrievalCatalogTask;private RetrievalCatalog? retrievalCatalog;
    private Task<RetainerRetrieval.ArtisanLists>? retrievalArtisanTask;private RetrievalPreset[] retrievalArtisan=[];private string retrievalArtisanNotice="";private bool retrievalArtisanRequested;private string[] retrievalArtisanLive=[];private string retrievalArtisanConnection="";
    private void LoadRetrievalCatalog(){if(retrievalCatalog!=null||retrievalCatalogTask!=null)return;retrievalCatalogTask=Task.Run(()=>{
        var names=DataManager.GetExcelSheet<Item>().Where(x=>x.RowId>0&&!string.IsNullOrWhiteSpace(x.Name.ToString())).ToDictionary(x=>x.RowId,x=>x.Name.ToString());
        var recipes=DataManager.GetExcelSheet<Recipe>().Where(r=>r.ItemResult.RowId>0&&r.AmountResult>0).Select(r=>new RetrievalRecipe(r.RowId,r.ItemResult.RowId,r.AmountResult,Enumerable.Range(0,r.Ingredient.Count).Where(i=>r.Ingredient[i].RowId>0&&r.AmountIngredient[i]>0).Select(i=>new RetrievalEntry(r.Ingredient[i].RowId,names.GetValueOrDefault(r.Ingredient[i].RowId,"Item"),r.AmountIngredient[i])).ToArray(),r.CraftType.Value.Name.ToString())).GroupBy(r=>r.ItemId).ToDictionary(g=>g.Key,g=>g.ToArray());
        var workshop=new List<RetrievalPreset>();foreach(var project in DataManager.GetExcelSheet<CompanyCraftSequence>()){
            if(project.ResultItem.RowId==0)continue;var rows=new List<RetrievalEntry>();foreach(var part in project.CompanyCraftPart.Where(x=>x.RowId>0))foreach(var process in part.Value.CompanyCraftProcess.Where(x=>x.RowId>0)){
                var step=process.Value;for(var i=0;i<step.SupplyItem.Count;i++){var supply=step.SupplyItem[i];if(supply.RowId==0)continue;var id=supply.Value.Item.RowId;var count=checked(step.SetQuantity[i]*step.SetsRequired[i]);if(id>0&&count>0)rows.Add(new(id,names.GetValueOrDefault(id,"Item"),count));}}
            if(rows.Count>0)workshop.Add(new(names.GetValueOrDefault(project.ResultItem.RowId,"Workshop "+project.RowId)+" · turn-ins",RetainerRetrieval.Combine(rows)));}
        var shark=workshop.Where(p=>p.Name.StartsWith("Shark-class ",StringComparison.OrdinalIgnoreCase)).ToArray();if(shark.Length==4)workshop.Add(new("Shark submarine (all 4 parts) · turn-ins",RetainerRetrieval.Combine(shark.SelectMany(p=>p.Items))));
        return new RetrievalCatalog(names,DataManager.GetExcelSheet<Item>().Where(x=>x.RowId>0).ToDictionary(x=>x.RowId,x=>(uint)x.Icon),recipes,workshop.OrderBy(x=>x.Name).ToArray());});}
    private RetrievalEntry? ResolveRetrievalItem(string name){if(retrievalCatalog==null)return null;if(uint.TryParse(name,out var id)&&retrievalCatalog.Names.TryGetValue(id,out var text))return new(id,text,1);var matches=retrievalCatalog.Names.Where(x=>x.Value.Equals(name,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();return matches.Length==1?new(matches[0].Key,matches[0].Value,1):null;}
    private void LoadRetrievalArtisan(){if(retrievalCatalog==null||retrievalArtisanTask!=null)return;
        var path=Path.Combine(Pi.ConfigFile.DirectoryName!,"Artisan.json");var recipes=retrievalCatalog.Recipes.Values.SelectMany(x=>x).ToDictionary(x=>x.Id);var names=retrievalCatalog.Names;
        retrievalArtisanLive=[];
        try{retrievalArtisanLive=Pi.GetIpcSubscriber<Dictionary<int,string>>("Artisan.GetLists").InvokeFunc().Values.Distinct().Order().ToArray();retrievalArtisanConnection="Artisan reports "+retrievalArtisanLive.Length+" live lists";}
        catch{retrievalArtisanConnection="Live Artisan list connection unavailable; reading saved files";}
        var premadePath=Path.Combine(Pi.ConfigFile.DirectoryName!,"Artisan","PremadeCraftsV2.dat");
        retrievalArtisanRequested=true;retrievalArtisanNotice="Reading saved Artisan lists…";
        retrievalArtisanTask=Task.Run(()=>{
            var presets=new List<RetrievalPreset>();var warnings=new List<string>();
            foreach(var file in new[]{path,premadePath})
            {
                if(!File.Exists(file)){if(file==path)warnings.Add("Artisan.json not found beside Companion configuration.");continue;}
                try{if(new FileInfo(file).Length>32*1024*1024)throw new IOException("File exceeds 32 MB.");
                    var read=RetainerRetrieval.ReadArtisan(File.ReadAllText(file),recipes,names);presets.AddRange(read.Lists);warnings.AddRange(read.Warnings);}
                catch(Exception e){warnings.Add(Path.GetFileName(file)+": "+e.Message);}
            }
            return new RetainerRetrieval.ArtisanLists(presets.ToArray(),warnings.ToArray());
        });
    }
}
