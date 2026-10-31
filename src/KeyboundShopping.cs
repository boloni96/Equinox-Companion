using System.Numerics;
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
        if(ImGui.Checkbox("Use Journal wishlist",ref journal)){config.KeyboundUseJournalWishlist=journal;SaveConfiguration();}
        config.KeyboundShopping.TryGetValue(character,out var plan);
        var unmatched=0;
        if(journal){
            var matches=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>c.ContentId==character.ToString()).DistinctBy(c=>c.Id).Take(2).ToArray();
            if(matches.Length!=1||matches[0].KeyboundWishlist is null){
                ImGui.TextWrapped("Waiting for this character's Journal wishlist. Deploy Journal 7.11.275 and let Companion sync.");
                if(ImGui.SmallButton("Open Journal"))Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/");
                return;
            }
            plan=KeyboundShoppingPolicy.WishlistPlan(matches[0].KeyboundWishlist!.Select(w=>(w.Name,w.Wanted,w.Obtained)),out unmatched);
        }
        var total=KeyboundShoppingPolicy.Total(plan);
        ImGui.TextUnformatted($"{(unmatched>0?"Known reward cost":"Rewards still to buy")}: {total:N0} MGC");
        if(unmatched>0)ImGui.TextWrapped($"{unmatched} wanted reward(s) have no matched shop price. Total and estimate cover matched rewards only.");
        if(keyboundMgc is {} balance&&keyboundMgcCharacter==character){
            var remaining=KeyboundShoppingPolicy.Remaining(total,balance);
            ImGui.TextUnformatted(total==0&&unmatched==0?"No pending rewards in this list":remaining==0?(unmatched>0?"Enough MGC for matched rewards":"Enough MGC for your list"):$"Still to earn: {remaining:N0} MGC");
            if(config.KeyboundRoundEarnings.TryGetValue(character,out var samples)&&samples.Count>0){
                var rounds=KeyboundShoppingPolicy.Rounds(remaining,samples);
                if(rounds.HasValue)ImGui.TextDisabled($"~{rounds:N0} rounds · average {samples.Average(x=>(double)x):N0} MGC");
                if(ImGui.IsItemHovered())ImGui.SetTooltip($"Based on {samples.Count} observed rounds for this character. Earnings vary; capped rounds are excluded.");
            }else if(remaining>0)ImGui.TextDisabled("Round estimate after a completed round");
            if(keyboundMgcLimit is {} cap&&total>cap)
                ImGui.TextWrapped("Your list exceeds the wallet cap. Buy rewards along the way and reduce their quantities below.");
        }
        if(!ImGui.CollapsingHeader("Reward shopping list"))return;
        ImGui.TextWrapped(journal?"Follows this character's Wanted and Obtained choices after Journal sync. Mark purchases in the Journal.":"Quantities still to buy for this character. Set 0 for owned or unwanted rewards. Purchases are marked manually.");
        if(journal){
            if(ImGui.Button("Open Journal"))Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/");
            if(ImGui.Button("Copy to custom list")){
                config.KeyboundShopping[character]=new(plan!);config.KeyboundUseJournalWishlist=false;SaveConfiguration();return;
            }
        }
        ImGui.BeginDisabled(journal);
        if(ImGui.Button("One of each")){plan=KeyboundShoppingPolicy.Prizes.ToDictionary(p=>p.Key,p=>1);config.KeyboundShopping[character]=plan;SaveConfiguration();}
        ImGui.SameLine();
        if(ImGui.Button("Clear list")){plan=[];config.KeyboundShopping[character]=plan;SaveConfiguration();}
        ImGui.EndDisabled();
        ImGui.TextDisabled("One of each: 6,380 MGC");
        var visible=ImGui.BeginChild("KeyboundPrizeList",new Vector2(0,ImGui.GetTextLineHeightWithSpacing()*16),true);
        if(visible)foreach(var prize in KeyboundShoppingPolicy.Prizes){
            if(journal&&KeyboundShoppingPolicy.Quantity(plan,prize.Key)==0)continue;
            ImGui.PushID(prize.Key);
            ImGui.TextWrapped(prize.Name);
            var qty=KeyboundShoppingPolicy.Quantity(plan,prize.Key);
            ImGui.BeginDisabled(journal);
            ImGui.SetNextItemWidth(ImGui.GetFontSize()*6);
            if(ImGui.InputInt("##quantity",ref qty,1,10)){
                plan??=KeyboundShoppingPolicy.Prizes.ToDictionary(p=>p.Key,p=>1);
                plan[prize.Key]=Math.Clamp(qty,0,999);config.KeyboundShopping[character]=plan;SaveConfiguration();
                keyboundTimingEditUntil=DateTimeOffset.UtcNow.AddMilliseconds(250);
            }
            if(ImGui.IsItemActive())keyboundTimingEditUntil=DateTimeOffset.UtcNow.AddMilliseconds(250);
            ImGui.SameLine();ImGui.TextUnformatted($"{prize.Cost:N0} each");
            ImGui.SameLine();
            if(ImGui.SmallButton("Have it")){
                plan??=KeyboundShoppingPolicy.Prizes.ToDictionary(p=>p.Key,p=>1);
                plan[prize.Key]=0;config.KeyboundShopping[character]=plan;SaveConfiguration();
            }
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Exclude this reward from the remaining shopping list.");
            ImGui.EndDisabled();ImGui.Separator();ImGui.PopID();
        }
        ImGui.EndChild();
        if(ImGui.SmallButton("Reset round estimate")){config.KeyboundRoundEarnings.Remove(character);SaveConfiguration();}
        if(ImGui.SmallButton("Official reward list"))Dalamud.Utility.Util.OpenLink("https://eu.finalfantasyxiv.com/lodestone/special/keybound_brawler/");
    }
}
