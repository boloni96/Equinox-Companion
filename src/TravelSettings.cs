using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private void DrawTravelSettings()
    {
        var nearby=config.NearbyTravelMenu;
        if(ImGui.Checkbox("Nearby travel menu",ref nearby)){config.NearbyTravelMenu=nearby;SaveConfiguration();}
        ImGui.TextWrapped("Optional shortcuts beside usable aetherytes, shards and transport NPCs. Disabled by default.");
        var position=Math.Clamp(config.NearbyTravelMenuPosition,0,4);
        ImGui.SetNextItemWidth(180);
        if(ImGui.Combo("Menu position",ref position,"Top\0Bottom\0Left\0Right\0Free position\0")){config.NearbyTravelMenuPosition=position;SaveConfiguration();}
        var suggestions=config.TravelCommandSuggestions;
        if(ImGui.Checkbox("Show /et command suggestions",ref suggestions)){config.TravelCommandSuggestions=suggestions;SaveConfiguration();}
        ImGui.TextWrapped("Click a suggestion or use Ctrl+1–9 to fill the chat command. Press Enter to travel. Disabled by default.");
        ImGui.Separator();
        ImGui.TextUnformatted("Ordinary travel");
        var city=Math.Clamp(config.TravelCity,0,3);
        ImGui.SetNextItemWidth(220);
        if(ImGui.Combo("Travel city",ref city,"Automatic\0Limsa Lominsa\0Gridania\0Ul'dah\0")){config.TravelCity=city;SaveConfiguration();}
        ImGui.TextWrapped("Default: Gridania. Preferred World Visit city for house visits, gardening routes and other ordinary travel. Applies when the journey needs a city teleport.");
        ImGui.TextWrapped("A nearby usable main aetheryte is reused. If your preferred city is locked, an unlocked city is used. Direct estate teleports keep going straight to the estate.");
        ImGui.Separator();ImGui.TextUnformatted("Shopping travel");
        DrawShoppingDestination();
        ImGui.TextWrapped("Default: Limsa Lominsa. Travel from shopping lists uses the shopping city and arrival point above. This is saved separately from ordinary travel.");
    }
}
