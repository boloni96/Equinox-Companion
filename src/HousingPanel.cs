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

    private sealed record LocalHousingRow(Actor Actor,string Location,HousingCharacterRows Houses);
    private readonly UiProjectionCache<LocalHousingRow[]> localHousingCache=new(0);
    private LocalHousingRow[] BuildLocalHousingRows()
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try {
            var index=HousingDisplayIndex.Build(config.Discoveries,config.Houses);
            return OrderedLocalCharacters().Select(a=>new LocalHousingRow(a,HomeLocation(a),
                index.Characters.GetValueOrDefault(a.ContentId)??new HousingCharacterRows([],[]))).ToArray();
        } finally {TrackPerformance("My Empire history index",started);}
    }
    private void DrawHousing()
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try { DrawHousingContents(); }
        finally { TrackPerformance("My Empire draw",started); }
    }
    private void DrawHousingContents()
    {
        ImGui.TextWrapped("My Empire");
        ImGui.TextColored(Green, "0–7 days"); ImGui.SameLine();
        ImGui.TextColored(Orange, "8–30"); ImGui.SameLine();
        ImGui.TextColored(Red, "31–45"); ImGui.SameLine();
        ImGui.TextColored(Purple, "45+ DEMOLISHED?");
        ImGui.TextWrapped("Based on recorded eligible entries. Hover an estate for details. Other players' visits and demolition suspensions may change the actual deadline.");
        ImGui.TextDisabled("Name bar: left = Private | right = FC. Character order: Settings.");
        ImGui.InputText("Find character / server", ref housingSearch, 100);
        var actors = localHousingCache.Get(config,$"{configurationRevision}:{config.Discoveries.Count}:{config.Houses.Count}:{config.Tending.Count}:{config.Planting.Count}:{(Player.IsLoaded?Player.ContentId:0)}",Environment.TickCount64,BuildLocalHousingRows);
        if (actors.Length == 0) ImGui.TextWrapped("No visible characters. Log in to record them, or restore hidden characters in Settings.");
        var now = DateTimeOffset.UtcNow;
        foreach (var row in actors)
        {
            var actor=row.Actor;var location=row.Location;
            if (!string.IsNullOrWhiteSpace(housingSearch) && !(actor.Name + " " + location).Contains(housingSearch, StringComparison.OrdinalIgnoreCase)) continue;
            ImGui.PushID(actor.ContentId);
            var estates = row.Houses.Estates;
            HousingBand? EstateBand(string type) => SummarizeBands(estates.Where(e => e.Estate.House!.Type == type)
                .Select(e => HousingStatus.Band(e.Entry, now)));
            var expanded = DrawSplitHeader(actor.Name + " · " + location + "###character", EstateBand("Private house"), EstateBand("Free Company house"),
                estates.Select(e => EntryHover(e.Estate.House!.Type, e.Estate.Address!.Ward, e.Estate.Address.Plot, e.Entry, now)));
            if (expanded)
            {
                ImGui.TextDisabled(location);
                var sharedCharacter=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).FirstOrDefault(c=>c.ContentId==actor.ContentId);
                if(sharedCharacter is not null)DrawCharacterTravel(sharedCharacter);
                if (estates.Length == 0) ImGui.TextWrapped("No owned estate confirmed yet. Open its placard and enter; FC details may need the member list opened once.");
                var firstEstate = true;
                foreach (var estateRow in estates)
                {
                    if (!firstEstate) { ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing(); }
                    firstEstate = false;
                    var estate=estateRow.Estate;var h = estate.House!; var a = WithAddressNames(estate.Address!);
                    var entry = estateRow.Entry;
                    var ownEntry = estateRow.OwnEntry;
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
                        ImGui.TextUnformatted(ownEntry is null ? "This character: no entry recorded" : $"This character entered: {DisplayTime.Local(ownEntry.Value)}");
                        ImGui.TextUnformatted(entry is null ? "Eligible entry: unknown" : $"Last recorded eligible entry: {DisplayTime.Local(entry.Value)}");
                        if (entry is not null) ImGui.TextUnformatted($"45-day estimate: {DisplayTime.Local(entry.Value.AddDays(45))}");
                        ImGui.TextUnformatted("Estimated—check in game. DEMOLISHED? does not confirm loss.");
                        ImGui.TextUnformatted("Only recorded owner / FC-member interior entries count.");
                        ImGui.EndTooltip();
                    }
                    ImGui.TextDisabled($"{a.DistrictName} · W{a.Ward} P{a.Plot} · {h.Size}");
                    if (entry is not null)
                    {
                        var age = Math.Max(0, (now - entry.Value).TotalDays);
                        var elapsed = age < 1 ? $"{Math.Max(0, (int)(now - entry.Value).TotalHours)}h" : $"{(int)age}d";
                        ImGui.TextWrapped($"Last eligible entry: {elapsed} ago · {DisplayTime.Local(entry.Value)}");
                        ImGui.TextColored(colour, age > 45 ? "45-day estimate passed—check in game" : $"About {Math.Max(0, (int)Math.Ceiling(45 - age))} days until the 45-day estimate");
                    }
                    else ImGui.TextWrapped("No eligible entry recorded. Countdown unknown.");
                    ImGui.Spacing();
                }
                var other = row.Houses.OtherVisits;
                if (other.Length > 0 && ImGui.TreeNode($"Other visits ({other.Length}) · ownership unconfirmed###other-visits"))
                {
                    var height = ImGui.GetTextLineHeightWithSpacing() * Math.Min(other.Length, 6) + ImGui.GetStyle().WindowPadding.Y * 2;
                    if (ImGui.BeginChild("OtherVisitsScroll", new Vector2(0, height), true))
                    {
                        foreach (var v in other)
                        {
                            var a = WithAddressNames(v.Address);
                            ImGui.TextWrapped($"{a.WorldName} · {a.DistrictName} · W{a.Ward} P{a.Plot} · {DisplayTime.Local(v.ObservedAt)}");
                        }
                    }
                    ImGui.EndChild();
                    ImGui.TreePop();
                }
            }
            ImGui.PopID();
        }
        ImGui.Separator();
        ImGui.TextDisabled("Local plugin records only. Website-only characters are not downloaded.");
    }
}
