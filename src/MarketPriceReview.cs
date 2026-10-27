namespace EquinoxCompanion;

// A bounded summary attached to the exact selling list, never a new stock or visit observation.
public sealed record MarketPriceReview(string StockKey,DateTimeOffset CheckedAt,DateTimeOffset ValidUntil,
    int Updates,int Unknown,int Review,int Excluded,string Source,DateTimeOffset? ConfirmedAt=null);

public static class MarketPriceReviews
{
    public static string Key(IEnumerable<MarketStock> stock)=>string.Join(";",stock.OrderBy(i=>i.Slot).Select(i=>
        FormattableString.Invariant($"{i.Slot}:{i.ItemId}:{(i.Hq?1:0)}:{i.Quantity}:{i.Price}:{i.Materia??"?"}")));
    public static bool Valid(MarketPriceReview? r,MarketStock[] stock,DateTimeOffset now)=>r==null||
        r.StockKey==Key(stock)&&r.CheckedAt.Year>=2020&&r.CheckedAt<=now.AddMinutes(5)&&r.ValidUntil>=r.CheckedAt&&r.ValidUntil<=r.CheckedAt.AddDays(1)&&
        r.Updates is >=0 and <=20&&r.Unknown is >=0 and <=20&&r.Review is >=0 and <=20&&r.Excluded is >=0 and <=20&&r.Updates+r.Unknown+r.Review+r.Excluded<=stock.Length&&
        r.Source is "Live" or "Universalis" or "Mixed" or "None"&&
        (r.ConfirmedAt==null||r.ConfirmedAt.Value.Year>=2020&&r.ConfirmedAt<=r.CheckedAt);
    public static MarketPriceReview? Merge(MarketPriceReview? a,MarketPriceReview? b,MarketStock[] stock)
    {
        var key=Key(stock);if(a?.StockKey!=key)a=null;if(b?.StockKey!=key)b=null;
        var latest=a==null?b:b==null?a:a.CheckedAt>=b.CheckedAt?a:b;
        return latest==null?null:latest with {ConfirmedAt=a?.ConfirmedAt>b?.ConfirmedAt?a.ConfirmedAt:b?.ConfirmedAt??a?.ConfirmedAt};
    }
    public static MarketPriceReview Build(MarketStock[] stock,MarketPriceReview? previous,
        Func<MarketStock,(SellerDecision Decision,DateTimeOffset Checked,DateTimeOffset Expires,bool Live)> read,
        ISet<uint> excluded,DateTimeOffset now)
    {
        var updates=0;var unknown=0;var review=0;var ignored=0;var live=0;var cached=0;
        var oldest=now;var latest=DateTimeOffset.MinValue;var expires=now.AddDays(1);
        foreach(var item in stock){
            if(excluded.Contains(item.ItemId)){ignored++;continue;}
            var x=read(item);if(x.Checked>latest)latest=x.Checked;if(x.Expires<expires)expires=x.Expires;
            if(x.Checked>now||x.Expires<now||x.Decision.Status is SellerStatus.Unknown or SellerStatus.Checking){unknown++;continue;}
            if(x.Live){live++;if(x.Checked<oldest)oldest=x.Checked;}else cached++;
            if(x.Decision.NeedsAttention)updates++;
            else if(x.Decision.Status!=SellerStatus.Competitive)review++;
        }
        // The minimum live receipt time requires every active listing to have
        // been checked after an adventure began. A cache fetch can never confirm it.
        DateTimeOffset? confirmed=stock.Length>ignored&&live==stock.Length-ignored&&updates+unknown+review==0?oldest:null;
        var key=Key(stock);if(previous?.StockKey==key&&previous.ConfirmedAt!=null&&(confirmed==null||previous.ConfirmedAt>confirmed))confirmed=previous.ConfirmedAt;
        var source=live>0?(cached>0?"Mixed":"Live"):cached>0?"Universalis":"None";
        var next=new MarketPriceReview(key,latest==DateTimeOffset.MinValue?now:latest,expires<=now?now.AddMinutes(5):expires>now.AddDays(1)?now.AddDays(1):expires,updates,unknown,review,ignored,source,confirmed);
        // Do not upload unchanged uncertainty every tick, or extend cache freshness.
        if(previous!=null&&previous with {CheckedAt=next.CheckedAt,ValidUntil=next.ValidUntil}==next&&previous.ValidUntil>=now)return previous;
        return next with {CheckedAt=now,ValidUntil=next.ValidUntil<now?now:next.ValidUntil};
    }
}
