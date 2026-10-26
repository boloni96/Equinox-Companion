using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private MarketRetainer[]? pairedMarketLocal;
    private SharedRoster? pairedMarketRoster;
    private MarketRetainer[] pairedMarketRows=[];
    private Dictionary<string,uint>? pairedMarketWorlds;
    private static string MarketRetainerLabel(MarketRetainer r)=>r.Name+(r.Level is >0?$" · Lv {r.Level} {r.Job}":"");
    private bool MatchesMarketScope(MarketRetainer r)=>(marketPerson.Length==0||PairedMarketView.PersonKey(r)==marketPerson)&&
        (marketAccount.Length==0||PairedMarketView.AccountKey(r)==marketAccount);
    private bool MatchesMarketOwner(ulong characterId)
    {
        if(marketPerson.Length==0&&marketAccount.Length==0)return true;
        var r=MarketRetainers.FirstOrDefault(x=>x.CharacterId==characterId);
        if(r!=null)return MatchesMarketScope(r);
        return config.SharedRoster?.People.Any(p=>p.Characters.Any(c=>c.ContentId==characterId.ToString()&&
            (marketPerson.Length==0||p.Id==marketPerson)&&(marketAccount.Length==0||p.Id+"/"+SharedCharacterGrouping.AccountKey(c)==marketAccount)))==true;
    }
    private static void MarketScopeCombo(string id,string all,IEnumerable<(string Key,string Name)> values,ref string selected)
    {
        var options=values.DistinctBy(x=>x.Key).OrderBy(x=>x.Name).ToArray();
        var current=selected;
        ImGui.SetNextItemWidth(Math.Min(180,ImGui.GetContentRegionAvail().X));
        if(ImGui.BeginCombo(id,selected.Length==0?all:options.FirstOrDefault(x=>x.Key==current).Name??"Selected scope"))
        {
            if(ImGui.Selectable(all,selected.Length==0))selected="";
            foreach(var value in options)if(ImGui.Selectable(value.Name+"###"+id+value.Key,selected==value.Key))selected=value.Key;
            ImGui.EndCombo();
        }
    }
    private void MarketCharacterHeading(MarketRetainer r)
    {
        ImGui.TextUnformatted($"{r.Character} · {r.World}");
        MarketSameLine(160);ImGui.TextDisabled($"{r.Person} / {r.Account}");
        MarketHelp("Shared listings follow the Journal's person and account assignments. Switch uses the existing login/account checks.");
    }
    private string MarketDecisionLabel(MarketRetainer r,MarketStock item,SellerDecision decision)
    {
        if(decision.Status!=SellerStatus.Unknown)return decision.Status.ToString();
        return marketWatchCache.TryGetValue((r.WorldId,item.ItemId),out var data)
            ? data.Error.Length>0?"No usable data":data.Uploaded==default||DateTimeOffset.UtcNow-data.Uploaded>TimeSpan.FromMinutes(config.Seller.BackgroundMaxAgeMinutes)?"Old market data":"No listings":"Not checked";
    }
    private MarketRetainer[] MarketRetainers
    {
        get
        {
            var roster=config.SyncEnabled&&config.Seller.SyncToJournal&&config.PairingKey.Length==64?config.SharedRoster:null;
            if(ReferenceEquals(pairedMarketLocal,marketLedger.Retainers)&&ReferenceEquals(pairedMarketRoster,roster))return pairedMarketRows;
            pairedMarketWorlds??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().Where(w=>w.IsPublic)
                .ToDictionary(w=>w.Name.ToString(),w=>w.RowId,StringComparer.OrdinalIgnoreCase);
            var rows=PairedMarketView.Build(marketLedger.Retainers,roster,name=>pairedMarketWorlds.GetValueOrDefault(name),DateTimeOffset.UtcNow);
            pairedMarketLocal=marketLedger.Retainers;pairedMarketRoster=roster;
            if(!PairedMarketView.Same(rows,pairedMarketRows))pairedMarketRows=rows;
            return pairedMarketRows;
        }
    }
}
