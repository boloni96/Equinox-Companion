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
    private readonly BackgroundDisplayProjection<LocalHousingProjection> housingHistoryProjection=new();
    private LocalHousingProjection? ReadLocalHousingProjection()=>housingHistoryProjection.Get(config,
        $"{config.Discoveries.Count}:{config.Discoveries.LastOrDefault()?.Id}:{config.Houses.Count}:{config.Houses.LastOrDefault()?.EventId}:{config.Tending.Count}:{config.Tending.LastOrDefault()?.EventId}:{config.Planting.Count}:{config.Planting.LastOrDefault()?.EventId}",()=>{
            var history=config.Discoveries.ToArray();var visits=config.Houses.ToArray();var tending=config.Tending.ToArray();var planting=config.Planting.ToArray();
            return ()=>LocalHousingProjection.Build(history,visits,tending,planting);
        });
    private LocalHousingRow[] BuildLocalHousingRows(LocalHousingProjection data)
    {
        var ordered=OrderCharacters("local",data.Actors,a=>a.ContentId,a=>data.Owners.Contains(a.ContentId));
        if(Player.IsLoaded){var current=Player.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture);ordered=ordered.OrderBy(a=>a.ContentId==current?0:1).ToArray();}
        return ordered.Select(a=>new LocalHousingRow(a,HomeLocation(a),data.Index.Characters.GetValueOrDefault(a.ContentId)??new HousingCharacterRows([],[]))).ToArray();
    }
    private void DrawHousing()
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try { DrawHousingContents(); }
        finally { TrackPerformance("My Empire draw",started); }
    }
    private void DrawHousingContents()
    {
        ImGui.TextColored(Green, "0–7 days"); SameLineIfFits(ImGui.CalcTextSize("8–30").X);
        ImGui.TextColored(Orange, "8–30"); SameLineIfFits(ImGui.CalcTextSize("31–45").X);
        ImGui.TextColored(Red, "31–45"); SameLineIfFits(ImGui.CalcTextSize("45+ DEMOLISHED?").X);
        ImGui.TextColored(Purple, "45+ DEMOLISHED?");
        SettingsHelp("Based on recorded eligible entries. Other players' visits and demolition suspensions may change the actual deadline. Name bar: left = Private, right = FC. Character order: Settings.");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##EmpireCharacterSearch","Find character / world", ref housingSearch, 100);
        ImGui.BeginChild("EmpireCharacters",Vector2.Zero,false);
        var data=ReadLocalHousingProjection();
        if(data is null){ImGui.TextWrapped(housingHistoryProjection.Error.Length>0?housingHistoryProjection.Error:"Preparing house history…");ImGui.EndChild();return;}
        var actors=localHousingCache.Get(data,$"{configurationRevision}:{(Player.IsLoaded?Player.ContentId:0)}",Environment.TickCount64,()=>BuildLocalHousingRows(data));
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
        SettingsHelp("Local plugin records. Paired characters are in their Person tabs.",false);
        ImGui.EndChild();
    }
}
