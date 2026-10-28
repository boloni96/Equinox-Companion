using System.Text.Json;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record RetrievalCatalog(Dictionary<uint,string> Names,Dictionary<uint,RetrievalRecipe[]> Recipes,RetrievalPreset[] Workshop);
    private Task<RetrievalCatalog>? retrievalCatalogTask;private RetrievalCatalog? retrievalCatalog;
    private Task<RetrievalPreset[]>? retrievalArtisanTask;private RetrievalPreset[] retrievalArtisan=[];
    private void LoadRetrievalCatalog(){if(retrievalCatalog!=null||retrievalCatalogTask!=null)return;retrievalCatalogTask=Task.Run(()=>{
        var names=DataManager.GetExcelSheet<Item>().Where(x=>x.RowId>0&&!string.IsNullOrWhiteSpace(x.Name.ToString())).ToDictionary(x=>x.RowId,x=>x.Name.ToString());
        var recipes=DataManager.GetExcelSheet<Recipe>().Where(r=>r.ItemResult.RowId>0&&r.AmountResult>0).Select(r=>new RetrievalRecipe(r.RowId,r.ItemResult.RowId,r.AmountResult,Enumerable.Range(0,r.Ingredient.Count).Where(i=>r.Ingredient[i].RowId>0&&r.AmountIngredient[i]>0).Select(i=>new RetrievalEntry(r.Ingredient[i].RowId,names.GetValueOrDefault(r.Ingredient[i].RowId,"Item"),r.AmountIngredient[i])).ToArray(),r.CraftType.Value.Name.ToString())).GroupBy(r=>r.ItemId).ToDictionary(g=>g.Key,g=>g.ToArray());
        var workshop=new List<RetrievalPreset>();foreach(var project in DataManager.GetExcelSheet<CompanyCraftSequence>()){
            if(project.ResultItem.RowId==0)continue;var rows=new List<RetrievalEntry>();foreach(var part in project.CompanyCraftPart.Where(x=>x.RowId>0))foreach(var process in part.Value.CompanyCraftProcess.Where(x=>x.RowId>0)){
                var step=process.Value;for(var i=0;i<step.SupplyItem.Count;i++){var supply=step.SupplyItem[i];if(supply.RowId==0)continue;var id=supply.Value.Item.RowId;var count=checked(step.SetQuantity[i]*step.SetsRequired[i]);if(id>0&&count>0)rows.Add(new(id,names.GetValueOrDefault(id,"Item"),count));}}
            if(rows.Count>0)workshop.Add(new(names.GetValueOrDefault(project.ResultItem.RowId,"Workshop "+project.RowId)+" · turn-ins",RetainerRetrieval.Combine(rows)));}
        var shark=workshop.Where(p=>p.Name.StartsWith("Shark-class ",StringComparison.OrdinalIgnoreCase)).ToArray();if(shark.Length==4)workshop.Add(new("Shark submarine (all 4 parts) · turn-ins",RetainerRetrieval.Combine(shark.SelectMany(p=>p.Items))));
        return new RetrievalCatalog(names,recipes,workshop.OrderBy(x=>x.Name).ToArray());});}
    private RetrievalEntry? ResolveRetrievalItem(string name){if(retrievalCatalog==null)return null;if(uint.TryParse(name,out var id)&&retrievalCatalog.Names.TryGetValue(id,out var text))return new(id,text,1);var matches=retrievalCatalog.Names.Where(x=>x.Value.Equals(name,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();return matches.Length==1?new(matches[0].Key,matches[0].Value,1):null;}
    private void LoadRetrievalArtisan(){if(retrievalCatalog==null||retrievalArtisanTask!=null)return;
        var path=Path.Combine(Pi.ConfigFile.DirectoryName!,"Artisan.json");var recipes=retrievalCatalog.Recipes.Values.SelectMany(x=>x).ToDictionary(x=>x.Id);var names=retrievalCatalog.Names;
        retrievalArtisanTask=Task.Run(()=>{if(!File.Exists(path))throw new IOException("No saved Artisan configuration found on this client.");if(new FileInfo(path).Length>32*1024*1024)throw new IOException("Artisan configuration is too large to read safely.");using var doc=JsonDocument.Parse(File.ReadAllText(path));if(!doc.RootElement.TryGetProperty("NewCraftingLists",out var lists))throw new IOException("Unsupported Artisan list format. Use whole-list paste instead.");var result=new List<RetrievalPreset>();
            foreach(var list in lists.EnumerateArray()){var entries=new List<RetrievalEntry>();foreach(var item in list.GetProperty("Recipes").EnumerateArray()){
                if(item.TryGetProperty("ListItemOptions",out var options)&&options.ValueKind==JsonValueKind.Object&&options.TryGetProperty("Skipping",out var skip)&&skip.ValueKind==JsonValueKind.True)continue;
                var id=item.GetProperty("ID").GetUInt32();var count=item.GetProperty("Quantity").GetInt32();if(count<=0)continue;if(!recipes.TryGetValue(id,out var r))throw new IOException("Artisan list contains an unknown recipe: "+id);entries.Add(new(r.ItemId,names[r.ItemId],checked(count*r.Yield),id));}
                result.Add(new(list.GetProperty("Name").GetString()??"Artisan list",RetainerRetrieval.Combine(entries)));}return result.ToArray();});
    }
}
