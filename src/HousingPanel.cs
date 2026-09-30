using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private string housingSearch = "";
    private static readonly Vector4 Green = new(.35f, .85f, .50f, 1);
    private static readonly Vector4 Orange = new(1, .65f, .22f, 1);
    private static readonly Vector4 Red = new(1, .32f, .35f, 1);
    private static readonly Vector4 Purple = new(.78f, .49f, 1, 1);
    private static readonly Vector4 Grey = new(.65f, .68f, .72f, 1);

    private static string HomeLocation(Actor actor)
    {
        var world = DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(actor.HomeWorldId);
        if (world is null) return actor.HomeWorldName ?? $"World {actor.HomeWorldId}";
        var dc = world.Value.DataCenter.Value;
        var region = dc.Region.RowId switch { 1 => "Japan", 2 => "North America", 3 => "Europe", 4 => "Oceania", _ => "Region unknown" };
        return $"{world.Value.Name} · {dc.Name} · {region}";
    }

    private void DrawHousing()
    {
        ImGui.TextWrapped("Characters & housing");
        ImGui.TextColored(Green, "0–7 days"); ImGui.SameLine();
        ImGui.TextColored(Orange, "8–30"); ImGui.SameLine();
        ImGui.TextColored(Red, "31–45"); ImGui.SameLine();
        ImGui.TextColored(Purple, "45+ DEMOLISHED?");
        ImGui.TextWrapped("Based on recorded eligible entries. Hover an estate for details. Other players' visits and demolition suspensions may change the actual deadline.");
        ImGui.InputText("Find character / server", ref housingSearch, 100);
        var actors = config.Discoveries.Select(d => (d.At, d.Actor))
            .Concat(config.Houses.Select(h => (At: h.ObservedAt, h.Actor)))
            .Concat(config.Tending.Select(t => (At: t.ConfirmedAt, t.Actor)))
            .Concat(config.Planting.Select(p => (At: p.ConfirmedAt, p.Actor)))
            .Where(x => SyncValidation.ActorReady(x.Actor)).GroupBy(x => x.Actor.ContentId)
            .Select(g => g.OrderByDescending(x => x.At).First().Actor)
            .OrderBy(a => a.HomeWorldName).ThenBy(a => a.Name).ToArray();
        if (actors.Length == 0) ImGui.TextWrapped("No characters recorded yet. Log in and play normally to add them here.");
        var now = DateTimeOffset.UtcNow;
        foreach (var actor in actors)
        {
            var location = HomeLocation(actor);
            if (!string.IsNullOrWhiteSpace(housingSearch) && !(actor.Name + " " + location).Contains(housingSearch, StringComparison.OrdinalIgnoreCase)) continue;
            ImGui.PushID(actor.ContentId);
            if (ImGui.CollapsingHeader(actor.Name + " · " + (actor.HomeWorldName ?? WorldName(actor.HomeWorldId)), ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled(location);
                var estates = config.Discoveries.Where(d => d.Kind == "house.discovered" && d.Actor.ContentId == actor.ContentId && d.Address is not null && d.House is not null)
                    .GroupBy(d => d.Address!.HouseId).Select(g => g.OrderByDescending(d => d.At).First()).OrderBy(d => d.House!.Type).ToArray();
                if (estates.Length == 0) ImGui.TextWrapped("No owned estate confirmed yet. Open its placard and enter; FC details may need the member list opened once.");
                foreach (var estate in estates)
                {
                    var h = estate.House!; var a = WithAddressNames(estate.Address!);
                    var entry = HousingStatus.LastEligibleEntry(estate, config.Discoveries, config.Houses);
                    var ownEntry = config.Houses.Where(v => v.Actor.ContentId == actor.ContentId && v.Address.HouseId == a.HouseId && v.Kind == "house.entered")
                        .Select(v => (DateTimeOffset?)v.ObservedAt).Max();
                    var band = HousingStatus.Band(entry, now);
                    var colour = band switch { HousingBand.Recent => Green, HousingBand.Warning => Orange, HousingBand.Urgent => Red, HousingBand.Overdue => Purple, _ => Grey };
                    var label = band switch { HousingBand.Recent => "RECENT", HousingBand.Warning => "VISIT SOON", HousingBand.Urgent => "ENTER HOUSE", HousingBand.Overdue => "DEMOLISHED?", _ => "ENTRY UNKNOWN" };
                    var name = string.IsNullOrWhiteSpace(h.EstateName) ? "Estate name unknown" : h.EstateName;
                    var type = h.Type == "Free Company house" ? "FC" : "Private";
                    ImGui.TextColored(colour, $"[{type}] {name} · {label}");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(name);
                        ImGui.TextUnformatted($"{a.WorldName} · {a.DistrictName} · Ward {a.Ward} · Plot {a.Plot}");
                        ImGui.TextUnformatted($"{h.Type} · {h.Size}");
                        if (h.FreeCompany is { } fc)
                        {
                            ImGui.TextUnformatted($"FC: {fc.Name} <{fc.Tag}>");
                            ImGui.TextUnformatted($"FC master: {(string.IsNullOrWhiteSpace(fc.MasterName) ? "Unknown" : fc.MasterName)}");
                        }
                        else ImGui.TextUnformatted($"Owner: {actor.Name}");
                        ImGui.TextUnformatted(ownEntry is null ? "This character: no entry recorded" : $"This character entered: {ownEntry.Value.ToLocalTime():g}");
                        ImGui.TextUnformatted(entry is null ? "Eligible entry: unknown" : $"Last recorded eligible entry: {entry.Value.ToLocalTime():g}");
                        if (entry is not null) ImGui.TextUnformatted($"45-day estimate: {entry.Value.AddDays(45).ToLocalTime():g}");
                        ImGui.TextUnformatted("Estimated—check in game. DEMOLISHED? does not confirm loss.");
                        ImGui.TextUnformatted("Only recorded owner / FC-member interior entries count.");
                        ImGui.EndTooltip();
                    }
                    ImGui.TextDisabled($"{a.DistrictName} · W{a.Ward} P{a.Plot} · {h.Size}");
                    if (entry is not null)
                    {
                        var age = Math.Max(0, (now - entry.Value).TotalDays);
                        var elapsed = age < 1 ? $"{Math.Max(0, (int)(now - entry.Value).TotalHours)}h" : $"{(int)age}d";
                        ImGui.TextWrapped($"Last eligible entry: {elapsed} ago · {entry.Value.ToLocalTime():g}");
                        ImGui.TextColored(colour, age > 45 ? "45-day estimate passed—check in game" : $"About {Math.Max(0, (int)Math.Ceiling(45 - age))} days until the 45-day estimate");
                    }
                    else ImGui.TextWrapped("No eligible entry recorded. Countdown unknown.");
                    ImGui.Spacing();
                }
                var ids = estates.Select(d => d.Address!.HouseId).ToHashSet();
                var other = config.Houses.Where(v => v.Actor.ContentId == actor.ContentId && !ids.Contains(v.Address.HouseId))
                    .GroupBy(v => v.Address.HouseId).Select(g => g.OrderByDescending(v => v.ObservedAt).First()).ToArray();
                if (other.Length > 0 && ImGui.TreeNode("Other visits (ownership unconfirmed)"))
                {
                    foreach (var v in other)
                    {
                        var a = WithAddressNames(v.Address);
                        ImGui.TextWrapped($"{a.WorldName} · {a.DistrictName} · W{a.Ward} P{a.Plot} · {v.ObservedAt.ToLocalTime():g}");
                    }
                    ImGui.TreePop();
                }
            }
            ImGui.PopID();
        }
        ImGui.Separator();
        ImGui.TextDisabled("Local plugin records only. Website-only characters are not downloaded.");
    }
}
