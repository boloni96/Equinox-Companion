using System.Collections.Concurrent;
using System.Globalization;
using Dalamud.Game.Chat;
using Dalamud.Game.Network.Structures;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private sealed record MarketActor(ulong Id,string Name,uint World,string WorldName,uint HomeWorld,string HomeWorldName);
    private MarketActor? marketActor;
    private readonly Dictionary<uint,MarketSaleReceipt> marketSaleReceipts=[];
    private readonly ConcurrentQueue<(MarketActor Actor,uint Item,bool Hq,long Net,DateTimeOffset At)> marketSaleQueue=new();
    private readonly ConcurrentQueue<MarketExpense> marketPurchaseQueue=new();
    private sealed record PendingMarketPurchase(MarketActor Actor,uint Item,bool Hq,uint Quantity,long Price,long Tax,DateTimeOffset At);
    private PendingMarketPurchase? marketPurchase;
    private string marketImportPath="";
    private Task<MarketIncome[]>? marketImport;
    private Task<string>? marketExport;
    private string marketFileStatus="";
    private void InitializeMarketReceipts()
    {
        foreach(var id in new uint[]{745,748})try{
            var template=DataManager.GetExcelSheet<Lumina.Excel.Sheets.LogMessage>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(id)?.Text.ToMacroString()??"";
            if(MarketSaleReceipt.FromTemplate(id,template) is {} parser)marketSaleReceipts[id]=parser;
        }catch(Exception ex){Log.Debug(ex,"Market sale template unavailable");}
    }
    private void ObserveMarketSale(ILogMessage message)
    {
        if(!config.Seller.Enabled||marketSaleQueue.Count>=128||!marketSaleReceipts.TryGetValue(message.LogMessageId,out var receipt)||Volatile.Read(ref marketActor) is not {} actor)return;
        var values=new int?[Math.Min(16,(int)message.ParameterCount)];
        for(var i=0;i<values.Length;i++)if(message.TryGetIntParameter(i,out var n))values[i]=n;
        if(receipt.Read(values) is {} sale)marketSaleQueue.Enqueue((actor,sale.Item,sale.Hq,sale.Net,DateTimeOffset.UtcNow));
    }
    private void MarketPurchaseRequested(IMarketBoardPurchaseHandler request)
    {
        var actor=Volatile.Read(ref marketActor);if(actor==null||!config.Seller.Enabled)return;
        Volatile.Write(ref marketPurchase,new(actor,request.CatalogId%1_000_000,request.IsHq,request.ItemQuantity,request.PricePerUnit,request.TotalTax,DateTimeOffset.UtcNow));
    }
    private void MarketPurchased(IMarketBoardPurchase purchase)
    {
        var p=Interlocked.Exchange(ref marketPurchase,null);
        if(p==null||purchase.CatalogId%1_000_000!=p.Item||purchase.ItemQuantity!=p.Quantity||p.Quantity==0||p.Price<=0||DateTimeOffset.UtcNow-p.At>TimeSpan.FromSeconds(30))return;
        marketPurchaseQueue.Enqueue(new(Guid.NewGuid().ToString("N"),p.Actor.Id,p.Actor.Name,p.Actor.World,p.Actor.WorldName,p.Item,p.Hq,p.Quantity,p.Price,p.Tax,DateTimeOffset.UtcNow));
    }
    private void DrainMarketSales()
    {
        var actor=Volatile.Read(ref marketActor);
        if(!Player.IsLoaded)Volatile.Write(ref marketActor,null);
        else if(actor==null||actor.Id!=Player.ContentId||actor.World!=Player.CurrentWorld.RowId||actor.HomeWorld!=Player.HomeWorld.RowId)
            Volatile.Write(ref marketActor,new(Player.ContentId,Player.CharacterName,Player.CurrentWorld.RowId,WorldName(Player.CurrentWorld.RowId)??"",Player.HomeWorld.RowId,WorldName(Player.HomeWorld.RowId)??""));
        while(marketSaleQueue.TryDequeue(out var sale)){
            var matches=marketLedger.Retainers.Where(r=>r.CharacterId==sale.Actor.Id).SelectMany(r=>r.Stock.Where(x=>x.ItemId==sale.Item&&x.Hq==sale.Hq&&sale.Net<=x.Total&&sale.Net>=x.Total-x.Total/20-1).Select(x=>(r,x))).Take(2).ToArray();
            var one=matches.Length==1?matches[0]:default;
            var row=new MarketIncome(Guid.NewGuid().ToString("N"),sale.Actor.Id,sale.Actor.Name,sale.Actor.HomeWorld,sale.Actor.HomeWorldName,one.r?.Id??0,one.r?.Name??"Unassigned",sale.Item,SearchItemName(sale.Item),sale.Hq,one.x?.Quantity??0,one.x?.Total,sale.Net,sale.At,"Game sale message; net confirmed"+(one.r!=null?"; retainer inferred from one matching listing":"; retainer/quantity not observed"));
            marketLedger=marketLedger with {Income=[..marketLedger.Income,row]};DirtyMarket();
        }
        while(marketPurchaseQueue.TryDequeue(out var purchase)){marketLedger=marketLedger with {Expenses=[..marketLedger.Expenses??[],purchase]};DirtyMarket();}
        if(marketImport is {IsCompleted:true}){
            if(marketImport.IsCompletedSuccessfully){var existing=marketLedger.Income.Select(x=>x.Id).ToHashSet();var added=marketImport.Result.Where(x=>existing.Add(x.Id)).ToArray();marketLedger=marketLedger with {Income=[..marketLedger.Income,..added]};DirtyMarket();marketFileStatus=$"Imported {added.Length} sale records; duplicates skipped.";}
            else marketFileStatus="Import failed; existing records kept: "+marketImport.Exception?.GetBaseException().Message;
            marketImport=null;
        }
        if(marketExport is {IsCompleted:true}){marketFileStatus=marketExport.IsCompletedSuccessfully?"Export ready: "+marketExport.Result:"Export failed: "+marketExport.Exception?.GetBaseException().Message;marketExport=null;}
    }
    private void ImportAllaganSales()
    {
        if(marketImport!=null||marketLoad!=null||marketLoadFailed)return;
        var path=marketImportPath.Trim().Trim('"');
        marketImport=Task.Run(()=>{
            if(new FileInfo(path).Length>32*1024*1024)throw new IOException("CSV exceeds 32 MB.");
            using var reader=File.OpenText(path);
            return MarketSalesImport.Read(reader);
        });marketFileStatus="Importing in the background…";
    }
}
