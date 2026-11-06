using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private void DrawTravelSettings()
    {
        ImGui.TextUnformatted("Ordinary travel");
        var city=Math.Clamp(config.TravelCity,0,3);
        ImGui.SetNextItemWidth(220);
        if(ImGui.Combo("Travel city",ref city,"Automatic\0Limsa Lominsa\0Gridania\0Ul'dah\0")){config.TravelCity=city;SaveConfiguration();}
        ImGui.TextWrapped("Preferred World Visit city for house visits, gardening routes and other ordinary travel. Applies when the journey needs a city teleport.");
        ImGui.TextWrapped("A nearby usable main aetheryte is reused. If your preferred city is locked, an unlocked city is used. Direct estate teleports keep going straight to the estate.");
        ImGui.Separator();ImGui.TextUnformatted("Shopping travel");
        DrawShoppingDestination();
        ImGui.TextWrapped("Travel from shopping lists uses the shopping city and arrival point above. This is saved separately from ordinary travel.");
    }
}
