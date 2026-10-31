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
        var plan=keyboundMissingPlan;
        if(plan is null){
            ImGui.TextWrapped(keyboundPlanError.Length>0?keyboundPlanError:"Checking missing rewards…");
            DrawKeyboundBuyButton();return;
        }
        var total=KeyboundShoppingPolicy.Total(plan);
        ImGui.TextUnformatted($"Missing rewards: {total:N0} MGC");
        if(keyboundMgc is {} balance&&keyboundMgcCharacter==character){
            var remaining=KeyboundShoppingPolicy.Remaining(total,balance);
            ImGui.TextUnformatted(total==0?"No pending rewards in this list":remaining==0?"Enough MGC for your list":$"Still to earn: {remaining:N0} MGC");
            if(config.KeyboundRoundEarnings.TryGetValue(character,out var samples)&&samples.Count>0){
                var rounds=KeyboundShoppingPolicy.Rounds(remaining,samples);
                if(rounds.HasValue&&ImGui.IsItemHovered())ImGui.SetTooltip($"~{rounds:N0} rounds · average {samples.Average(x=>(double)x):N0} MGC from {samples.Count} observed rounds. Earnings vary.");
            }
            if(keyboundMgcLimit is {} cap&&total>cap)
                ImGui.TextWrapped("Your list exceeds the wallet cap. Buy rewards along the way and mark Obtained in the Journal.");
        }
        if(keyboundHeldRewards.Count>0)ImGui.TextWrapped($"{keyboundHeldRewards.Count} reward(s) held for verification · other rewards can be bought.");
        DrawKeyboundBuyButton();
    }
}

