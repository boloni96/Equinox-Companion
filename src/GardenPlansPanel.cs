using Dalamud.Bindings.ImGui;
using System.Numerics;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private PlantingGuideWindow plantingWindow = null!;
    private sealed class PlantingGuideWindow : Window
    {
        private readonly Plugin plugin;
        public PlantingGuideWindow(Plugin plugin) : base("Planting guide###EquinoxPlanting")
        {
            this.plugin = plugin;
            Size = new Vector2(720, 540);
            SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 280), MaximumSize = new Vector2(float.MaxValue) };
            AllowPinning = true;
            AllowBackgroundBlur = true;
        }
        public override void Draw() => plugin.DrawGardenPlans();
    }
    private string? plantingHouseId;
    private int plantingBatch = 1;
    private string? previousPlantingHouse;
    private void OnPlantingCommand(string command, string args)
    {
        nextRosterRead = default;
        plantingHouseId = config.SyncEnabled && config.PairingKey.Length == 64 ? SharedGardenLocation.Match(currentAddress, GardenPlanSources()) : null;
        if (plantingHouseId is null) { if(config.NotifyPlantingUnavailable) Chat.Print("[Equinox] /planting is available only at an identified paired house."); return; }
        plantingWindow.IsOpen = true;
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
        ImGui.TextWrapped("Bed numbers are positions; step numbers are planting order. Plant manually in game. Keep the website open to sync planting and care. Times use the last observed records.");
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
        var yields = config.SharedRoster?.GardenYields?.FirstOrDefault(y => y.HouseId == plantingHouseId && y.Batch == plantingBatch);
        if (yields is not null)
        {
            ImGui.TextWrapped("Harvest totals (if all beds are harvested)");
            ImGui.TextWrapped("Recorded crops: " + yields.Actual);
            if (!string.IsNullOrWhiteSpace(yields.Planned)) ImGui.TextWrapped("With saved plan: " + yields.Planned);
            ImGui.TextWrapped(yields.Seeds);
            ImGui.TextDisabled("Full-cycle potential, not inventory received. Kept crops count only if harvested.");
        }
        foreach (var plan in housePlans.Where(p => p.Batch == plantingBatch && p.At != DateTimeOffset.MinValue))
        {
            ImGui.PushID(plan.HouseId + ":" + plan.Batch);
            ImGui.TextWrapped($"{plan.HouseName} · {plan.World} · {plan.District} W{plan.Ward} P{plan.Plot} · Batch {plan.Batch}");
            {
                ImGui.TextWrapped($"Goal: {plan.Target} · Plan saved {plan.At.ToLocalTime():g}");
                if (ImGui.BeginTable("beds", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchSame))
                {
                    int[] layout = [1, 2, 3, 8, 0, 4, 7, 6, 5];
                    for (var position = 0; position < layout.Length; position++)
                    {
                        ImGui.TableNextColumn();
                        var number = layout[position];
                        if (number == 0) { ImGui.TextWrapped($"Garden plan\nBatch {plan.Batch}\nFollow step numbers"); continue; }
                        var bed = plan.Beds.FirstOrDefault(b => b.Bed == number);
                        ImGui.TextUnformatted($"Bed {number} · hover for details");
                        var hoverBed = ImGui.IsItemHovered();
                        if (bed is null) { ImGui.TextDisabled("Keep existing bed"); continue; }
                        var green = bed.Status is "confirmed" or "starter";
                        var red = bed.Status == "replant";
                        var color = green ? new Vector4(.45f, .9f, .6f, 1) : red ? new Vector4(1, .35f, .4f, 1) : bed.Status == "different" ? new Vector4(1, .65f, .35f, 1) : new Vector4(.85f, .85f, .9f, 1);
                        if (green || red) ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(green ? new Vector4(.12f,.42f,.22f,.32f) : new Vector4(.65f,.12f,.16f,.35f)));
                        ImGui.PushStyleColor(ImGuiCol.Text, color);
                        ImGui.TextWrapped(bed.Status == "confirmed" ? "Planted: matches plan" : red ? "Remove starter; replant now" : bed.Status == "different" ? "Planted differently" : bed.Status == "starter" ? "Starter planted; finish neighbours" : "Awaiting planting");
                        ImGui.PopStyleColor();
                        if (bed.Order > 0) ImGui.TextWrapped($"Step {bed.Order}" + (bed.ReplantOrder > 0 ? $" + {bed.ReplantOrder}" : ""));
                        ImGui.TextWrapped(bed.Crop);
                        ImGui.TextWrapped(bed.ReplantOrder > 0 && bed.Status is "planned" or "starter" ? $"Start: {bed.StarterSoil}" : bed.Soil);
                        if (bed.Ready) ImGui.TextColored(new Vector4(.45f,.9f,.6f,1), "Ready to harvest");
                        if (hoverBed)
                        {
                            ImGui.BeginTooltip();
                            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 27);
                            if (bed.ReplantOrder > 0) ImGui.TextWrapped($"Step {bed.Order}: temporary {bed.Crop} with {bed.StarterSoil}. After the other required plants are confirmed, step {bed.ReplantOrder}: remove only that starter and replant {bed.Crop} with {bed.Soil}. Its first seed and soil are consumed.");
                            ImGui.TextWrapped($"Final plan: {bed.Crop} · {bed.Soil}");
                            if (bed.CheckExisting) ImGui.TextWrapped("Existing crop was unknown when planning: check in game before replacing anything.");
                            ImGui.TextWrapped($"Actual: {bed.ActualCrop} · {bed.ActualSoil}");
                            if (bed.Planted is { } at) ImGui.TextWrapped($"Planted: {at.ToLocalTime():g}");
                            if (!bed.Ready && bed.HarvestAt is { } harvest) ImGui.TextWrapped($"Maturity estimate: {harvest.ToLocalTime():g}");
                            if (!bed.Ready && bed.NextTend is { } next) ImGui.TextWrapped($"Tending due: {next.ToLocalTime():g}");
                            if (bed.Watered is { } watered) ImGui.TextWrapped($"Last tended / planted: {watered.ToLocalTime():g}");
                            ImGui.TextWrapped("Green confirms crop and soil, not a guaranteed cross. Colours refresh from website sync.");
                            ImGui.PopTextWrapPos();
                            ImGui.EndTooltip();
                        }
                    }
                    ImGui.EndTable();
                }
                var supplies = plan.Beds.SelectMany(b => b.ReplantOrder > 0
                    ? new[] { b.Crop + " seeds", b.Crop + " seeds", b.Soil, b.StarterSoil }
                    : new[] { b.Crop + " seeds", b.Soil }).GroupBy(x => x).Select(g => $"{g.Count()} × {g.Key}");
                ImGui.TextWrapped("Full plan supplies: " + string.Join(", ", supplies));
                ImGui.TextWrapped("Tips: keep compatible neighbours in place until the new seeds are planted. A mature neighbour can still be used. Harvest gives the planted crop, with possible extra crossbred seeds. Follow the saved layout; swap the starting seed on the website before planting if wanted.");
                ImGui.TextWrapped("Crossbreeding is not guaranteed. The first seed in an empty patch has no neighbour to cross with. Estimates include recorded fishmeal; confirm maturity in game.");
            }
            ImGui.PopID();
        }
    }
}
