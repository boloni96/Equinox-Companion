using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private KeyboundRoundEarnings keyboundEarnings=new();
    private void ObserveKeyboundShopping(bool results)
    {
        if(keyboundMgc is not {} balance||keyboundMgcCharacter==0)return;
        if(keyboundEarnings.Observe(Client.TerritoryType==1359,results,balance,keyboundMgcLimit) is not {} earned)return;
        if(!config.KeyboundRoundEarnings.TryGetValue(keyboundMgcCharacter,out var samples))
            config.KeyboundRoundEarnings[keyboundMgcCharacter]=samples=[];
        samples.Add(earned);if(samples.Count>20)samples.RemoveRange(0,samples.Count-20);
        SaveConfiguration();
    }
    private void DrawKeyboundShopping()
    {
        var character=Player.ContentId;
        if(!Client.IsLoggedIn||character==0)return;
        var journal=config.KeyboundUseJournalWishlist;
        Dictionary<string,int>? plan=null; // Without Journal goals, target one of every reward.
        var unmatched=0;
        if(journal){
            var matches=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>c.ContentId==character.ToString()).DistinctBy(c=>c.Id).Take(2).ToArray();
            if(matches.Length!=1||matches[0].KeyboundWishlist is null){
                ImGui.TextWrapped("Waiting for Journal wishlist sync.");
                return;
            }
            plan=KeyboundShoppingPolicy.WishlistPlan(matches[0].KeyboundWishlist!.Select(w=>(w.Name,w.Wanted,w.Obtained)),out unmatched);
        }
        var total=KeyboundShoppingPolicy.Total(plan);
        ImGui.TextUnformatted($"{(unmatched>0?"Known reward cost":"Target")}: {total:N0} MGC");
        if(unmatched>0)ImGui.TextWrapped($"{unmatched} wanted reward(s) have no matched shop price. Total and estimate cover matched rewards only.");
        if(keyboundMgc is {} balance&&keyboundMgcCharacter==character){
            var remaining=KeyboundShoppingPolicy.Remaining(total,balance);
            ImGui.TextUnformatted(total==0&&unmatched==0?"No pending rewards in this list":remaining==0?(unmatched>0?"Enough MGC for matched rewards":"Enough MGC for your list"):$"Still to earn: {remaining:N0} MGC");
            if(config.KeyboundRoundEarnings.TryGetValue(character,out var samples)&&samples.Count>0){
                var rounds=KeyboundShoppingPolicy.Rounds(remaining,samples);
                if(rounds.HasValue&&ImGui.IsItemHovered())ImGui.SetTooltip($"~{rounds:N0} rounds · average {samples.Average(x=>(double)x):N0} MGC from {samples.Count} observed rounds. Earnings vary.");
            }
            if(keyboundMgcLimit is {} cap&&total>cap)
                ImGui.TextWrapped("Your list exceeds the wallet cap. Buy rewards along the way and mark Obtained in the Journal.");
        }
    }
}
