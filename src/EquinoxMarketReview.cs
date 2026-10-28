using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset marketReviewNext;
    private void UpdateMarketReviews(DateTimeOffset now)
    {
        if(now<marketReviewNext)return;marketReviewNext=now.AddSeconds(5);
        var changed=false;
        var namesByWorld=MarketRetainers.GroupBy(r=>r.WorldId).ToDictionary(g=>g.Key,g=>g.Select(r=>r.Name).ToHashSet(StringComparer.Ordinal));
        // Only locally observed retainers are uploaded. Paired downloads stay a projection.
        var rows=marketLedger.Retainers.Select(r=>{
            var review=MarketPriceReviews.Build(r.Stock,r.PriceReview,item=>{
                if(sellerReviews.TryGetValue((r.Id,item.Slot),out var live)&&live.Item.SamePricingItem(item)&&live.Item.Price==item.Price&&now-live.At<=TimeSpan.FromMinutes(5))
                    return (live.Decision,live.At,live.At.AddMinutes(5),true);
                if(config.Seller.BackgroundChecks&&marketWatchCache.TryGetValue((r.WorldId,item.ItemId),out var cached)){
                    var names=namesByWorld.GetValueOrDefault(r.WorldId)??new HashSet<string>();
                    return (MarketWatchPolicy.Assess(item,r.WorldId,cached,names,config.Seller,now,IsSellerLevelOneOutfit(item.ItemId)),cached.Checked,
                        cached.Error.Length==0?cached.Uploaded.AddMinutes(Math.Clamp(config.Seller.BackgroundMaxAgeMinutes,5,1440)):cached.Checked.AddMinutes(5),false);
                }
                return (new SellerDecision(SellerStatus.Unknown,null,"Waiting for a usable price check.","None"),now,now.AddMinutes(5),false);
            },config.Seller.ExcludedItems,now);
            if(review==r.PriceReview)return r;changed=true;return r with {PriceReview=review};
        }).ToArray();
        if(changed){marketLedger=marketLedger with {Retainers=rows};DirtyMarket();}
    }
    private void DrawAdventureMarketActions(AdventureTask task)
    {
        if(task.Rule?.Kind!="market"||!config.Seller.Enabled||!ulong.TryParse(task.Rule.RetainerId,out var id))return;
        var retainer=MarketRetainerById(id);if(retainer==null||retainer.SharedCharacterId!=task.Rule.Cid)return;
        if(ImGui.SmallButton("Market"))OpenMarketSelling(id,false);
        MarketHelp("Open this retainer’s listings and price suggestions. Background checks use its home world.");
        ImGui.SameLine();var mine=Player.IsLoaded&&Player.ContentId==retainer.CharacterId&&Player.CurrentWorld.RowId==retainer.WorldId;
        ImGui.BeginDisabled(!mine);
        if(ImGui.SmallButton("Seller")){sellerScope=1;config.Seller.SelectedRetainers=[id];SaveConfiguration();OpenEquinoxSeller();}
        ImGui.EndDisabled();MarketHelp(mine?"Open Seller with only this retainer selected. Use Check prices or Update prices at a summoning bell.":"Switch to this character and return to their home world first.");
    }
}
