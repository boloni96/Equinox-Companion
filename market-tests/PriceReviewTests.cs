using EquinoxCompanion;
static class PriceReviewTests
{
    internal static void Run(Action<string,bool> check,DateTimeOffset now)
    {
        var first=new MarketStock(0,10,"Item",false,99,500,now,Materia:"");
        var stock=new[]{first,first with {Slot=1,ItemId=11,Hq=true,Materia="219,234"}};
        var ok=new SellerDecision(SellerStatus.Competitive,null,"Checked","Live listings");
        MarketPriceReview Build(bool live,SellerDecision? decision=null,MarketPriceReview? old=null,DateTimeOffset? at=null)=>MarketPriceReviews.Build(stock,old,_=>(decision??ok,at??now,(at??now).AddMinutes(5),live),new HashSet<uint>(),now);
        var cached=Build(false);check("Universalis all-clear cannot complete adventure",cached.ConfirmedAt==null&&cached.Source=="Universalis");
        var live=Build(true);check("all live competitive listings confirm",live.ConfirmedAt==now&&MarketPriceReviews.Valid(live,stock,now));
        var update=Build(true,new(SellerStatus.Update,499,"Lower","Live listings"));check("remaining update is not complete",update.Updates==2&&update.ConfirmedAt==null);
        check("safety skip remains review",Build(true,new(SellerStatus.Skipped,null,"Safety rule","Live listings")).Review==2);
        check("unknown remains unknown",Build(false,new(SellerStatus.Unknown,null,"Old","Universalis")).Unknown==2);
        check("undercut with blocked price stays pending",Build(true,ok with {Undercut=true}).Updates==2);
        check("expired live comparison cannot confirm",Build(true,at:now.AddMinutes(-6)).ConfirmedAt==null);
        var mixed=MarketPriceReviews.Build(stock,null,i=>(ok,now,now.AddMinutes(5),i.Slot==0),new HashSet<uint>(),now);
        check("mixed cache/live cannot confirm",mixed.Source=="Mixed"&&mixed.ConfirmedAt==null);
        var partial=MarketPriceReviews.Build(stock,null,i=>(ok,i.Slot==0?now.AddMinutes(-2):now,now.AddMinutes(5),true),new HashSet<uint>(),now);
        check("completion uses oldest listing receipt",partial.ConfirmedAt==now.AddMinutes(-2));
        var ignored=MarketPriceReviews.Build(stock,null,_=>(ok,now,now.AddMinutes(5),true),new HashSet<uint>{10,11},now);
        check("excluding everything does not fabricate completion",ignored.Excluded==2&&ignored.ConfirmedAt==null);
        var retained=Build(false,old:live);check("later cache preserves earlier live proof",retained.ConfirmedAt==now);
        check("changed price invalidates proof",MarketPriceReviews.Merge(live,null,[first with {Price=1}])==null);
        check("changed materia invalidates proof",MarketPriceReviews.Merge(live,null,[first,stock[1] with {Materia=""}])==null);
        check("same stock different display order has same key",MarketPriceReviews.Key(stock)==MarketPriceReviews.Key(stock.Reverse()));
        check("stable uncertain review avoids repeated upload",Build(false,new(SellerStatus.Unknown,null,"Old","Universalis"),Build(false,new(SellerStatus.Unknown,null,"Old","Universalis")))==Build(false,new(SellerStatus.Unknown,null,"Old","Universalis")));
        check("summary rejects changed stock",!MarketPriceReviews.Valid(live,[first],now));
        check("summary rejects oversized counters",!MarketPriceReviews.Valid(live with {Updates=int.MaxValue,Unknown=int.MaxValue},stock,now));
        check("summary rejects future proof",!MarketPriceReviews.Valid(live with {ConfirmedAt=now.AddHours(1)},stock,now));
        var batch=new MarketWatchBatch([new(20,10,now,now,[]),new(20,11,default,now,[],"Missing")],TimeSpan.FromSeconds(10));
        check("partial response reports unavailable items",MarketWatchPolicy.Summary(batch).Contains("1/2")&&MarketWatchPolicy.Summary(batch).Contains("retry"));
        check("partial failed item retries in one minute",MarketWatchPolicy.NextCheck(batch,11,now,15)==now.AddMinutes(1));
        check("partial successful item keeps normal interval",MarketWatchPolicy.NextCheck(batch,10,now,15)==now.AddMinutes(15));
        check("retry respects service backoff",MarketWatchPolicy.NextCheck(new([],TimeSpan.FromMinutes(10),"Busy"),10,now,15)==now.AddMinutes(10));
        check("key distinguishes unknown from plain materia",MarketPriceReviews.Key([first])!=MarketPriceReviews.Key([first with {Materia=null}]));
    }
}
