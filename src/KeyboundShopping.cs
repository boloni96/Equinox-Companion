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
        config.KeyboundShopping.TryGetValue(character,out var plan);
        var total=KeyboundShoppingPolicy.Total(plan);
        ImGui.TextUnformatted($"Rewards still to buy: {total:N0} MGC");
        if(keyboundMgc is {} balance&&keyboundMgcCharacter==character){
            var remaining=KeyboundShoppingPolicy.Remaining(total,balance);
            ImGui.TextUnformatted(remaining==0?"Enough MGC for your list":$"Still to earn: {remaining:N0} MGC");
            if(config.KeyboundRoundEarnings.TryGetValue(character,out var samples)&&samples.Count>0){
                var rounds=KeyboundShoppingPolicy.Rounds(remaining,samples);
                if(rounds.HasValue)ImGui.TextDisabled($"~{rounds:N0} rounds · average {samples.Average(x=>(double)x):N0} MGC");
                if(ImGui.IsItemHovered())ImGui.SetTooltip($"Based on {samples.Count} observed rounds for this character. Earnings vary; capped rounds are excluded.");
            }else if(remaining>0)ImGui.TextDisabled("Round estimate after a completed round");
            if(keyboundMgcLimit is {} cap&&total>cap)
                ImGui.TextWrapped("Your list exceeds the wallet cap. Buy rewards along the way and reduce their quantities below.");
        }
        if(!ImGui.CollapsingHeader("Reward shopping list"))return;
        ImGui.TextWrapped("Quantities still to buy for this character. Set 0 for owned or unwanted rewards. Purchases are marked manually.");
        if(ImGui.Button("One of each")){plan=KeyboundShoppingPolicy.Prizes.ToDictionary(p=>p.Key,p=>1);config.KeyboundShopping[character]=plan;SaveConfiguration();}
        ImGui.SameLine();
        if(ImGui.Button("Clear list")){plan=[];config.KeyboundShopping[character]=plan;SaveConfiguration();}
        ImGui.TextDisabled("One of each: 6,380 MGC");
        var visible=ImGui.BeginChild("KeyboundPrizeList",new Vector2(0,ImGui.GetTextLineHeightWithSpacing()*16),true);
        if(visible)foreach(var prize in KeyboundShoppingPolicy.Prizes){
            ImGui.PushID(prize.Key);
            ImGui.TextWrapped(prize.Name);
            var qty=KeyboundShoppingPolicy.Quantity(plan,prize.Key);
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
            ImGui.Separator();ImGui.PopID();
        }
        ImGui.EndChild();
        if(ImGui.SmallButton("Reset round estimate")){config.KeyboundRoundEarnings.Remove(character);SaveConfiguration();}
        if(ImGui.SmallButton("Official reward list"))Dalamud.Utility.Util.OpenLink("https://eu.finalfantasyxiv.com/lodestone/special/keybound_brawler/");
    }
}
