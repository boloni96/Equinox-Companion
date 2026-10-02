using Dalamud.Bindings.ImGui;
using System.Numerics;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string? requestedTab;
    private string? plantingHouseId;
    private int plantingBatch = 1;
    private string? previousPlantingHouse;
    private void OnPlantingCommand(string command, string args)
    {
        nextRosterRead = default;
        plantingHouseId = config.SyncEnabled && config.PairingKey.Length == 64 ? SharedGardenLocation.Match(currentAddress, GardenPlanSources()) : null;
        if (plantingHouseId is null) { Chat.Print("[Equinox] /planting is available only at an identified paired house."); return; }
        visible = true; requestedTab = "gardens";
    }
    private SharedGardenPlan[] GardenPlanSources()
    {
        var savedPlans = config.SharedRoster?.GardenPlans ?? [];
        var homesWithoutPlans = (config.SharedRoster?.People ?? []).SelectMany(p => p.Characters).SelectMany(c => c.Houses).DistinctBy(h => h.Id)
            .Where(h => !savedPlans.Any(p => p.HouseId == h.Id)).Select(h => new SharedGardenPlan(h.Id, h.Name, h.World, h.District, h.Ward, h.Plot, 1, "No saved plan", DateTimeOffset.MinValue, [], h.GameHouseId, h.Size == "Large" ? 3 : h.Size == "Medium" ? 2 : 1));
        return savedPlans.Concat(homesWithoutPlans).ToArray();
    }
    private void DrawGardenPlans()
    {
        var plans = GardenPlanSources();
        plantingHouseId = config.SyncEnabled && config.PairingKey.Length == 64 ? SharedGardenLocation.Match(currentAddress, plans) : null;
        if (plantingHouseId is null) { ImGui.TextWrapped("Visit an identified paired house to view its planting guide. No guide is shown while outside or loading."); return; }
        ImGui.TextWrapped("Follow the numbered beds and plant manually in game. Keep the website open to sync planting and care. Times use the last observed records.");
        ImGui.TextDisabled("Same layout as the website. Match bed orientation to the installed patch.");
        if (previousPlantingHouse != plantingHouseId) { plantingBatch = 1; previousPlantingHouse = plantingHouseId; }
        var housePlans = plans.Where(p => p.HouseId == plantingHouseId).ToArray();
        var capacity = housePlans.Length == 0 ? 0 : Math.Clamp(housePlans.Max(p => Math.Max(p.Capacity, p.Batch)), 1, 20);
        for (var batch = 1; batch <= capacity; batch++)
        {
            if (batch > 1) ImGui.SameLine();
            if (ImGui.Selectable($"Batch {batch}", plantingBatch == batch, ImGuiSelectableFlags.None, new Vector2(90, 24))) plantingBatch = batch;
        }
        if (capacity > 0 && !housePlans.Any(p => p.Batch == plantingBatch && p.At != DateTimeOffset.MinValue)) ImGui.TextWrapped($"Batch {plantingBatch} has no saved plan. Choose its centre button on the website to plan it.");
        foreach (var plan in housePlans.Where(p => p.Batch == plantingBatch && p.At != DateTimeOffset.MinValue))
        {
            ImGui.PushID(plan.HouseId + ":" + plan.Batch);
            if (ImGui.CollapsingHeader($"{plan.HouseName} · {plan.World} · {plan.District} W{plan.Ward} P{plan.Plot} · Batch {plan.Batch}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextWrapped($"Goal: {plan.Target} · Plan saved {plan.At.ToLocalTime():g}");
                if (ImGui.BeginTable("beds", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchSame))
                {
                    int[] layout = [1, 2, 3, 8, 0, 4, 7, 6, 5];
                    for (var position = 0; position < layout.Length; position++)
                    {
                        ImGui.TableNextColumn();
                        var number = layout[position];
                        if (number == 0) { ImGui.TextWrapped($"Garden plan\nBatch {plan.Batch}\nPlant beds in numbered order"); continue; }
                        var bed = plan.Beds.FirstOrDefault(b => b.Bed == number);
                        ImGui.TextUnformatted($"Bed {number}");
                        if (bed is null) { ImGui.TextDisabled("Keep existing bed"); continue; }
                        var color = bed.Status == "confirmed" ? new Vector4(.45f, .9f, .6f, 1) : bed.Status == "different" ? new Vector4(1, .65f, .35f, 1) : new Vector4(.85f, .85f, .9f, 1);
                        ImGui.PushStyleColor(ImGuiCol.Text, color);
                        ImGui.TextWrapped(bed.Status == "confirmed" ? "Confirmed: matches plan" : bed.Status == "different" ? "Planted differently" : "Planned: awaiting planting");
                        ImGui.PopStyleColor();
                        ImGui.TextWrapped($"Plant: {bed.Crop}\nSoil: {bed.Soil}");
                        if (bed.Status == "different") ImGui.TextWrapped($"Actual: {bed.ActualCrop}\nActual soil: {bed.ActualSoil}");
                        if (bed.Planted is { } at) ImGui.TextWrapped($"Planted: {at.ToLocalTime():g}");
                        if (bed.Ready) ImGui.TextColored(new Vector4(.45f, .9f, .6f, 1), "Ready to harvest");
                        else if (bed.HarvestAt is { } harvest) ImGui.TextWrapped($"Maturity estimate: {harvest.ToLocalTime():g}");
                        if (!bed.Ready && bed.NextTend is { } next) ImGui.TextWrapped(next <= DateTimeOffset.UtcNow ? "Tending due now" : $"Tend in {Math.Ceiling((next - DateTimeOffset.UtcNow).TotalMinutes / 60):0}h · {next.ToLocalTime():g}");
                        if (bed.Watered is { } watered) ImGui.TextWrapped($"Last tended / planted: {watered.ToLocalTime():g}");
                    }
                    ImGui.EndTable();
                }
                ImGui.TextWrapped("Crossbreeding is not guaranteed. The first seed in an empty patch has no neighbour to cross with. Estimates include recorded fishmeal; confirm maturity in game.");
            }
            ImGui.PopID();
        }
    }
}
