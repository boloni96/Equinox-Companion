using FFXIVClientStructs.FFXIV.Client.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Dictionary<(ulong Character,ulong Retainer),long> marketHistoryVersions=[];
    private string? sellerHistoryMenuLabel;
    private string SellerHistoryMenuLabel()
    {
        if(sellerHistoryMenuLabel!=null)return sellerHistoryMenuLabel;
        var english=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>(Dalamud.Game.ClientLanguage.English);
        var rows=english.Where(r=>r.Text.ToString().Trim().TrimEnd('.') is "View sale history" or "View sales history").ToArray();
        var labels=rows.Select(r=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRowOrDefault(r.RowId)?.Text.ToString()??"").Where(x=>x.Length>0).Distinct().ToArray();
        sellerHistoryMenuLabel=labels.Length==1?labels[0]:"";
        return sellerHistoryMenuLabel;
    }
    private sealed partial class SellerHistoryVisit
    {
        public ulong Retainer;public long Baseline;public bool Requested;public DateTimeOffset Started;
        public void Reset(){Retainer=0;Baseline=0;Requested=false;Started=default;}
    }
    private unsafe SellerPreparation ReadSellerSaleHistory(ulong id,SellerHistoryVisit visit)
    {
        if(!SellerActive(id))return new(false,Error:"Stopped: retainer changed while reading sale history.");
        if(marketHistoryHook?.IsEnabled!=true)return new(false,Error:"Sale-history capture is unavailable. Existing Market records were kept; re-enable Market & Seller and retry.");
        var key=(Player.ContentId,id);
        if(visit.Retainer!=id){visit.Reset();visit.Retainer=id;visit.Started=DateTimeOffset.UtcNow;visit.Baseline=marketHistoryVersions.GetValueOrDefault(key);}
        if(marketHistoryVersions.GetValueOrDefault(key)>visit.Baseline){
            var history=SellerAddon("RetainerHistory");if(history!=null){history->Close(true);return new(false,"Closing confirmed sale history");}
            if(SellerAddon("SelectString")==null){SellerRetainerTalk(id);return new(false,"Returning from sale history");}
            return new(true);
        }
        if(DateTimeOffset.UtcNow-visit.Started>TimeSpan.FromSeconds(20))return new(false,Error:"Sale history was not confirmed: "+marketHistoryStatus+" Existing records were kept; retry Check prices.");
        if(visit.Requested)return new(false,"Reading retainer sale history · waiting for confirmed records");
        // This read-only run owns the current retainer's list/history windows.
        foreach(var name in new[]{"RetainerSellList","RetainerHistory"}){var existing=SellerAddon(name);if(existing!=null){existing->Close(true);return new(false,"Opening retainer sale history");}}
        var menu=(AddonSelectString*)SellerAddon("SelectString");if(menu==null){SellerRetainerTalk(id);return new(false,"Waiting for retainer menu");}
        var label=SellerHistoryMenuLabel();if(label.Length==0)return new(false,Error:"The game's sale-history menu label could not be verified. Existing records were kept.");
        var matches=new List<int>();
        for(var i=0;i<Math.Min(20,(int)menu->PopupMenu.PopupMenu.EntryCount);i++)if(TravelMenuText(menu->PopupMenu.PopupMenu.EntryNames[i].Value)==label)matches.Add(i);
        if(matches.Count!=1)return new(false,"Waiting for View sale history");
        visit.Requested=true;menu->FireCallbackInt(matches[0]);return new(false,"Reading retainer sale history");
    }
}
