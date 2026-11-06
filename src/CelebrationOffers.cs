namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private KeyboundOffer[]? celebrationOffers;
    private IEnumerable<bool> PrepareCelebrationOffers()
    {
        if(celebrationOffers!=null)yield break;
        if(keyboundOffers!=null){celebrationOffers=keyboundOffers;yield break;}
        // Completion needs reward IDs, not the organizer's full armoire catalogue.
        // Read the item sheet incrementally, once, on the framework thread.
        var prizes=KeyboundShoppingPolicy.Prizes.ToDictionary(p=>KeyboundShoppingPolicy.Normalize(p.Name));
        var offers=new List<KeyboundOffer>();
        foreach(var item in DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English)){
            if(prizes.TryGetValue(KeyboundShoppingPolicy.Normalize(item.Name.ToString()),out var prize))
                offers.Add(new(item.RowId,prize.Key,prize.Name,prize.Cost));
            yield return true;
        }
        if(offers.Count!=prizes.Count||offers.Select(o=>o.Key).Distinct().Count()!=prizes.Count)
            throw new InvalidOperationException("Reward catalogue does not match the current game.");
        celebrationOffers=offers.ToArray();
    }
}
