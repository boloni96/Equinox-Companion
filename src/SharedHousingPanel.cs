using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Task<RosterResult>? rosterTask;
    private DateTimeOffset? rosterReceivedAt;
    private string rosterTaskKey = "";
    private DateTimeOffset nextRosterRead;
    private string rosterStatus = "Waiting for paired Cloudflare profiles.";
    private string sharedSearch = "";
    private void UpdateSharedRoster(DateTimeOffset now)
    {
        if (rosterTask?.IsCompleted == true)
        {
            if (rosterTaskKey == config.PairingKey && rosterTask.IsCompletedSuccessfully)
            {
                var r = rosterTask.Result; rosterStatus = r.Status;
                if(r.Roster is not null||r.NotModified)rosterReceivedAt=now;
                if(r.Roster is not null||r.NotModified)registrationFreshKey=rosterTaskKey;
                if (r.Roster is null && !r.NotModified) { errorJournal.Record("shared-roster", r.Status); nextRosterRead=now.AddSeconds(30); }
                if (r.Roster is not null) { if((config.SharedRoster?.ProtocolVersion??0)<13&&r.Roster.ProtocolVersion>=13){autoRetainerForceRead=true;autoRetainerCharacters.Clear();nextAutoRetainerRead=default;} config.SharedRoster = r.Roster; SaveConfiguration(); }
                if (r.Unauthorized) { config.SharedRoster = null; SaveConfiguration(); }
            }
            if (rosterTask.IsFaulted) errorJournal.Record("shared-roster", "Shared roster task failed.", exceptionType: rosterTask.Exception?.GetBaseException().GetType().Name);
            rosterTask = null;
        }
        if ((!visible && !(bidWindow?.IsOpen??false) && !(adventureWindow?.IsOpen??false) && !FastGardenSync && !config.RefreshSharedInBackground && !RegistrationNeedsRoster) || !config.SyncEnabled || config.PairingKey.Length != 64 || rosterTask is not null || now < nextRosterRead) return;
        nextRosterRead = now.AddSeconds(FastGardenSync ? 2 : 15);
        rosterTaskKey = config.PairingKey;
        rosterTask = sync.ReadRoster(rosterTaskKey, config.SharedRoster?.Revision);
    }
    private void DrawSharedStatus()
    {
        ImGui.TextDisabled(rosterStatus);
        if (config.SharedRoster is { } r) ImGui.TextDisabled($"Shared journal saved: {DisplayTime.Local(r.Updated)}");
        if (ImGui.SmallButton("Refresh shared profiles")) nextRosterRead = default;
        ImGui.SameLine(); ImGui.TextDisabled("Drag tabs to reorder");
    }
    private static HousingBand? SummarizeBands(IEnumerable<HousingBand> source)
    {
        var bands = source.ToArray(); if (bands.Length == 0) return null;
        var worst = bands.Max();
        return worst == HousingBand.Recent && bands.Contains(HousingBand.Unknown) ? HousingBand.Unknown : worst;
    }
    private static Vector4 BandColour(HousingBand? band) => band switch {
        HousingBand.Recent => Green, HousingBand.Warning => Orange, HousingBand.Urgent => Red,
        HousingBand.Overdue => Purple, HousingBand.Unknown => Grey, _ => new(.30f,.32f,.35f,1)
    };
    private static int HouseDisplayOrder(string type) => type == "Private house" ? 0 : type == "Free Company house" ? 1 : 2;
    private static string BandLabel(HousingBand? band) => band switch {
        HousingBand.Recent => "Recent (0–7 days)", HousingBand.Warning => "8–30 days", HousingBand.Urgent => "31–45 days",
        HousingBand.Overdue => "DEMOLISHED? — estimate", HousingBand.Unknown => "Entry unknown / paused", _ => "No house recorded"
    };
    private static (string Text, HousingBand Band) EntryHover(string type, int ward, int plot, DateTimeOffset? entry, DateTimeOffset now, bool paused = false)
        => ($"{(type == "Private house" ? "Private" : "FC")} · W{ward} P{plot} — Last eligible entry: {(entry is { } at ? DisplayTime.Local(at) : "Unknown")}", paused ? HousingBand.Unknown : HousingStatus.Band(entry, now));
    private static bool DrawSplitHeader(string label, HousingBand? privateBand, HousingBand? fcBand, IEnumerable<(string Text, HousingBand Band)> entryDetails)
    {
        var pos = ImGui.GetCursorScreenPos(); var width = Math.Max(1, ImGui.GetContentRegionAvail().X); var height = ImGui.GetFrameHeight();
        static uint Background(HousingBand? b) { var c = BandColour(b);return ImGui.ColorConvertFloat4ToU32(new(c.X*.38f,c.Y*.38f,c.Z*.38f,1)); }
        var draw = ImGui.GetWindowDrawList(); var middle = pos.X+width/2;
        draw.AddRectFilled(pos,new Vector2(middle,pos.Y+height),Background(privateBand));
        draw.AddRectFilled(new Vector2(middle,pos.Y),new Vector2(pos.X+width,pos.Y+height),Background(fcBand));
        draw.AddLine(new Vector2(middle,pos.Y),new Vector2(middle,pos.Y+height),ImGui.ColorConvertFloat4ToU32(new Vector4(.1f,.1f,.1f,1)));
        ImGui.PushStyleColor(ImGuiCol.Header,Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,new Vector4(1,1,1,.10f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,new Vector4(1,1,1,.18f));
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.One);
        var open = ImGui.CollapsingHeader(label);
        ImGui.PopStyleColor(4);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            var any = false;
            foreach (var detail in entryDetails) { ImGui.TextColored(BandColour(detail.Band), detail.Text); any = true; }
            if (!any) ImGui.TextDisabled("No house recorded.");
            ImGui.EndTooltip();
        }
        return open;
    }
    private SharedRoster? eligibilityRoster;
    private readonly Dictionary<(string Character,string House),bool> housingEligibilityCache=[];
    private bool CountsForCharacter(SharedCharacter c, SharedHouse h)
    {
        if(!ReferenceEquals(eligibilityRoster,config.SharedRoster)){eligibilityRoster=config.SharedRoster;housingEligibilityCache.Clear();}
        var key=(c.Id,h.Id);
        if(housingEligibilityCache.TryGetValue(key,out var eligible))return eligible;
        eligible=SharedHousingEligibility.CountsForCharacter(c,h,(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters));
        housingEligibilityCache[key]=eligible;return eligible;
    }
    private void DrawSharedPerson(SharedPerson person)
    {
        ImGui.PushID("shared-"+person.Id);
        ImGui.InputText("Find character / server",ref sharedSearch,100);
        SettingsHelp("Shared Journal characters. Name bar: left = Private, right = FC. Hover an estate for entry details.");
        ImGui.BeginChild("SharedCharacters",Vector2.Zero,false);
        var now=DateTimeOffset.UtcNow;
        var characters = OrderedSharedCharacters(person);
        var searching = !string.IsNullOrWhiteSpace(sharedSearch);
        var visibleCharacters = characters.Where(c => !searching || (c.Name+" "+c.World+" "+c.Dc+" "+c.Region+" "+c.Account).Contains(sharedSearch,StringComparison.OrdinalIgnoreCase)).ToArray();
        config.SharedAccountExpanded ??= [];
        // Reuse one eligibility/status snapshot for both account and category bars.
        var headerDetails=visibleCharacters.ToDictionary(c=>c.Id,c=>c.Houses
            .Where(h=>CountsForCharacter(c,h))
            .Select(h=>(Type:h.Type,Detail:EntryHover(h.Type,h.Ward,h.Plot,h.LastEntry,now,h.Paused)))
            .ToArray());
        bool GroupHeader(string label,IEnumerable<SharedCharacter> rows)
        {
            var entries=rows.SelectMany(c=>headerDetails[c.Id]).ToArray();
            return DrawSplitHeader(label,
                SummarizeBands(entries.Where(x=>x.Type=="Private house").Select(x=>x.Detail.Band)),
                SummarizeBands(entries.Where(x=>x.Type=="Free Company house").Select(x=>x.Detail.Band)),
                entries.Select(x=>x.Detail));
        }
        foreach (var account in visibleCharacters.GroupBy(SharedCharacterGrouping.AccountKey))
        {
            ImGui.PushID("account-" + account.Key);
            var key = person.Id + ":" + account.Key;
            var expanded = searching || config.SharedAccountExpanded.GetValueOrDefault(key);
            ImGui.SetNextItemOpen(expanded, ImGuiCond.Always);
            var open = GroupHeader($"{account.First().Account} · {account.Count()} characters###account",account);
            if (!searching && open != expanded)
            {
                config.SharedAccountExpanded[key] = open;
                SaveConfiguration();
            }
            if (open)
            {
                ImGui.Indent();
                foreach (var group in new[] { "Regulars", "Floaters", "Empty", "Pending sync" })
                {
                    var grouped = account.Where(c => SharedCharacterGrouping.Group(c) == group).ToArray();
                    if (grouped.Length == 0) continue;
                    ImGui.Spacing(); ImGui.Separator();
                    var groupKey=key+":group:"+group;
                    var groupExpanded=searching||config.SharedAccountExpanded.GetValueOrDefault(groupKey,true);
                    ImGui.SetNextItemOpen(groupExpanded,ImGuiCond.Always);
                    var groupOpen=GroupHeader($"{group} · {grouped.Length}###group-{group}",grouped);
                    if(!searching&&groupOpen!=groupExpanded){config.SharedAccountExpanded[groupKey]=groupOpen;SaveConfiguration();}
                    if(!groupOpen)continue;
                    foreach (var c in grouped)
                    {
            ImGui.PushID(c.Id);
            string HouseLabel(SharedHouse h) => SharedHousePresentation.Label(c, h, h.Type == "Private house" && CountsForCharacter(c, h));
            var houses = c.Houses.OrderBy(h => SharedHousePresentation.Order(HouseLabel(h))).ToArray();
            var timerHouses = houses.Where(h => HouseLabel(h) != "Shared" && CountsForCharacter(c, h)).ToArray();
            HousingBand? Status(string type) => SummarizeBands(timerHouses.Where(h=>h.Type==type).Select(h=>h.Paused?HousingBand.Unknown:HousingStatus.Band(h.LastEntry,now)));
            if (DrawSplitHeader(c.Name+" · "+SharedLocation(c)+"###character",Status("Private house"),Status("Free Company house"),
                timerHouses.Select(h=>EntryHover(h.Type,h.Ward,h.Plot,h.LastEntry,now,h.Paused))))
            {
                ImGui.TextDisabled(c.World+" · "+c.Dc+" · "+c.Region+" · "+c.Account);
                if (ImGui.SmallButton("Open character in Journal"))
                    Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/#character=" + Uri.EscapeDataString(c.Id));
                DrawCharacterTravel(c);
                if(c.Houses.Length==0)ImGui.TextDisabled("No house recorded in the shared Journal.");
                var firstEstate = true;
                foreach(var h in houses)
                {
                    if (!firstEstate) { ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing(); }
                    firstEstate = false;
                    var label = HouseLabel(h);
                    var band=h.Paused?HousingBand.Unknown:HousingStatus.Band(h.LastEntry,now);
                    ImGui.TextColored(BandColour(band),$"[{label}] {(string.IsNullOrWhiteSpace(h.Name)?"Estate name unknown":h.Name)} · {BandLabel(band)}");
                    if(ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted($"{h.World} · {h.District} · W{h.Ward} P{h.Plot} · {h.Size}");
                        ImGui.TextUnformatted($"Owner / FC master: {h.OwnerName}");
                        ImGui.TextUnformatted($"Estate type: {h.Type}");
                        if (label == "Shared") ImGui.TextUnformatted(h.Type == "Private house" ? "Shared access / ownership unconfirmed. Only the owner resets the timer." : "Shared access / FC membership unconfirmed. Only FC members reset the timer.");
                        if(!string.IsNullOrWhiteSpace(h.FcName))ImGui.TextUnformatted($"FC: {h.FcName} <{h.FcTag}>");
                        if(h.LastEntry is not null)ImGui.TextUnformatted($"45-day estimate: {DisplayTime.Local(h.LastEntry.Value.AddDays(45))}");
                        ImGui.TextUnformatted("Based on shared recorded entries, not the game's live countdown.");
                        ImGui.EndTooltip();
                    }
                    ImGui.PushID(h.Id);ImGui.BeginDisabled(!Player.IsLoaded);if(ImGui.SmallButton("GO SEE"))VisitSharedHouse(h);ImGui.EndDisabled();ImGui.PopID();
                    ImGui.TextDisabled($"{h.District} · W{h.Ward} P{h.Plot} · {h.Size}");
                    ImGui.TextWrapped(h.Paused?"Demolition marked suspended in Journal.":h.LastEntry is null?"No eligible entry recorded.":$"Last eligible entry: {DisplayTime.Local(h.LastEntry.Value)} · {Math.Max(0,(int)(now-h.LastEntry.Value).TotalDays)} days ago");
                    ImGui.Spacing();
                }
            }
            ImGui.PopID();
                    }
                }
                ImGui.Unindent();
            }
            ImGui.PopID();
        }
        if (characters.Length > 0 && visibleCharacters.Length == 0) ImGui.TextDisabled("No characters match this search.");
        if(characters.Length==0)ImGui.TextDisabled("No visible characters in this profile. Check Settings for hidden characters.");
        ImGui.EndChild();
        ImGui.PopID();
    }
}
