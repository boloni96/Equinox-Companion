using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private IDtrBarEntry? marketBar;
    private DateTimeOffset marketBarNext;
    private string marketBarLast="";
    private void RemoveMarketBar(){marketBar?.Remove();marketBar=null;marketBarLast="";}
    private void OpenMarketSelling(ulong retainer=0,bool updatesOnly=true)
    {
        marketPerson=marketAccount=marketCharacter=marketRetainerFilter=marketWorldFilter=marketFilter="";
        marketFocusRetainer=retainer;marketAttentionOnly=false;marketUpdatesOnly=updatesOnly;marketPage=0;openMarketSelling=true;openMarketSettings=false;OpenEquinoxMarket();
    }
    private void UpdateMarketBar(DateTimeOffset now)
    {
        if(!config.Seller.Enabled||!config.Seller.ShowMarketBar){RemoveMarketBar();return;}
        if(now<marketBarNext)return;marketBarNext=now.AddSeconds(2);
        marketBar??=QuickLootBar.Get("Equinox Market");marketBar.Shown=Player.IsLoaded;
        var rows=marketLedger.Retainers.SelectMany(r=>r.Stock.Select(s=>(Retainer:r,Stock:s,Decision:ReviewFor(r.Id,s.Slot)))).ToArray();
        var updates=rows.Count(x=>x.Decision.NeedsAttention);var unknown=rows.Count(x=>x.Decision.Status is SellerStatus.Unknown or SellerStatus.Skipped or SellerStatus.Checking);
        var undercuts=rows.Count(x=>x.Decision.CanApply&&x.Decision.Price<x.Stock.Price);
        var estimated=rows.Count(x=>x.Decision.CanApply&&x.Decision.Source=="Universalis");
        var visits=marketLedger.Retainers.Count(MarketVisitDue);
        var label=rows.Length==0?"Market: no listings":$"Market: {updates} updates"+(visits>0?$" · {visits} visits":"")+(unknown>0?" · ?":"");
        var color=(ushort)(updates>0?17:visits>0?31:unknown>0||rows.Length==0?0:45);
        var key=label+"/"+color;
        if(key!=marketBarLast){var b=new SeStringBuilder();if(color!=0)b.AddUiForeground(color);b.AddText(label);if(color!=0)b.AddUiForegroundOff();marketBar.Text=b.Build();marketBarLast=key;}
        marketBar.Tooltip=new SeStringBuilder().AddText($"{updates} suggested price updates across {rows.Length} known listings\n{undercuts} lower-price suggestions · {estimated} based on Universalis\n{unknown} listings need a fresh or successful check\n{visits} retainers due for a precautionary visit\nClick: open Selling in game retainer order\nRight-click: Seller settings\nCached suggestions are verified live before repricing.").Build();
        marketBar.OnClick=e=>{if(e.ClickType==MouseClickType.Left)OpenMarketSelling(updatesOnly:visits==0);else if(e.ClickType==MouseClickType.Right)OpenSellerSettings();};
    }
}
