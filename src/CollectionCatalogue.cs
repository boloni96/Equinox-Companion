using System.Diagnostics;
using System.Text.Json;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Task<CatalogueEntry[][]>? catalogueLoad;
    private CatalogueEntry[][]? catalogueBase;
    private IEnumerator<Lumina.Excel.Sheets.Quest>? catalogueQuests;
    private IEnumerator<Lumina.Excel.Sheets.CharaMakeCustomize>? catalogueHair;
    private readonly List<CatalogueEntry> catalogueSeasonal = [];
    private readonly Dictionary<uint, HashSet<uint>> catalogueHairLinks = [];
    private DateTimeOffset catalogueRetry;
    private void PrepareCollectionCatalogue(DateTimeOffset now)
    {
        if(collectionGroups is not null || !config.SyncCollections || now < characterReadyAt || now < catalogueRetry) return;
        if(catalogueLoad is null)
        {
            var path = Path.Combine(Pi.AssemblyLocation.DirectoryName!, "collection-ids.json");
            catalogueLoad = Task.Run(() => (JsonSerializer.Deserialize<CatalogueEntry[]>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [])
                .Where(x => x.Id > 0).GroupBy(x => x.Category).Select(g => g.DistinctBy(x => x.Id).ToArray()).ToArray());
            collectionStatus = "Preparing collection catalogue…";
            return;
        }
        if(!catalogueLoad.IsCompleted) return;
        if(!catalogueLoad.IsCompletedSuccessfully)
        {
            _ = catalogueLoad.Exception; catalogueLoad = null; catalogueRetry = now.AddMinutes(1);
            collectionStatus = "Collection catalogue unavailable; retrying in one minute."; return;
        }
        if(catalogueBase is null)
        {
            catalogueBase = catalogueLoad.Result;
            catalogueQuests = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>(Dalamud.Game.ClientLanguage.English).GetEnumerator();
            catalogueHair = DataManager.GetExcelSheet<Lumina.Excel.Sheets.CharaMakeCustomize>(Dalamud.Game.ClientLanguage.English).GetEnumerator();
        }
        // Game sheet reads stay on the framework thread, with a short budget.
        var start = Stopwatch.GetTimestamp();
        do
        {
            if(catalogueQuests is not null)
            {
                if(catalogueQuests.MoveNext()) AddCatalogueQuest(catalogueQuests.Current);
                else { catalogueQuests.Dispose(); catalogueQuests = null; }
            }
            else if(catalogueHair is not null)
            {
                if(catalogueHair.MoveNext())
                {
                    var hair = catalogueHair.Current;
                    if(hair.HintItem.RowId > 0 && hair.UnlockLink > 0)
                    {
                        if(!catalogueHairLinks.TryGetValue(hair.HintItem.RowId, out var links)) catalogueHairLinks[hair.HintItem.RowId] = links = [];
                        links.Add(hair.UnlockLink);
                    }
                }
                else { catalogueHair.Dispose(); catalogueHair = null; }
            }
            else
            {
                hairstyleUnlocks = catalogueHairLinks.ToDictionary(x => x.Key, x => x.Value.ToArray());
                collectionGroups = catalogueBase.Concat(catalogueSeasonal.OrderBy(x => x.Id).Chunk(8)).ToArray();
                catalogueSeasonal.Clear(); catalogueHairLinks.Clear(); catalogueBase = null;
                collectionStatus = "Collection catalogue ready."; return;
            }
        } while(Stopwatch.GetElapsedTime(start).TotalMilliseconds < 1);
    }
    private void AddCatalogueQuest(Lumina.Excel.Sheets.Quest q)
    {
                if(q.Festival.RowId==0)return;
                var name=q.Name.ToString();var festival=q.Festival.Value.Name.ToString();
                if(string.IsNullOrWhiteSpace(name)||name.Length>100||name.Any(char.IsControl)||
                   string.IsNullOrWhiteSpace(festival)||festival.Length>100||festival.Any(char.IsControl))return;
                var rewards = new List<EventQuestReward>();
                var itemIds = q.OptionalItemReward.Select(x=>x.RowId).ToList();
                if(q.ItemRewardType is 1 or 3 or 5)itemIds.AddRange(q.Reward.Select(x=>x.RowId));
                foreach(var id in itemIds.Where(id=>id>0).Distinct().Take(12))
                {
                    var itemName=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(id)?.Name.ToString();
                    if(!string.IsNullOrWhiteSpace(itemName)&&itemName.Length<=100&&!itemName.Any(char.IsControl))rewards.Add(new(id,itemName));
                }
                catalogueSeasonal.Add(new("quest",q.RowId,0,new(q.RowId,name,q.Festival.RowId,festival,(int)q.ClassJobLevel[0],
                    q.PreviousQuest.Select(x=>x.RowId).Where(id=>id>0).ToArray(),rewards.ToArray())));
    }
}
