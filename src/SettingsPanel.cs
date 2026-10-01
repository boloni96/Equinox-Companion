using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool showSavedPairingKey;
    private readonly List<HouseObservation> pendingHouseNotices = [];
    private void UpdateHouseNotices(DateTimeOffset now)
    {
        if (!config.NotifyHouseEntries) { pendingHouseNotices.Clear(); return; }
        foreach (var visit in pendingHouseNotices.ToArray())
        {
            if (!Player.IsLoaded || Player.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture) != visit.Actor.ContentId)
            { pendingHouseNotices.Remove(visit); continue; }
            var estate = config.Discoveries.LastOrDefault(e => e.Kind == "house.discovered" && e.Actor.ContentId == visit.Actor.ContentId && e.Address?.HouseId == visit.Address.HouseId);
            // Give the estate observer a chance to identify a newly discovered house.
            if (estate is null && now - visit.ObservedAt < TimeSpan.FromSeconds(10)) continue;
            var message = HouseEntryNotice.Format(visit, estate, config.SharedRoster);
            if (message is not null) Chat.Print(new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddUiForeground(45).AddText(message).AddUiForegroundOff().Build());
            pendingHouseNotices.Remove(visit);
        }
    }
    private void DrawSettings()
    {
        if (!ImGui.BeginTabBar("SettingsSections")) return;
        if (ImGui.BeginTabItem("General"))
        {
            DrawGeneralSettings();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Characters"))
        {
            DrawCharacterOrderSettings();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Connection"))
        {
            DrawConnection();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Diagnostics"))
        {
            ImGui.TextWrapped("Errors and held-record reasons are saved automatically on this PC, even with the window closed. Repeated issues are limited to once every five minutes; the log keeps about 2 MB across two files.");
            ImGui.TextWrapped(errorJournal.FilePath);
            if (ImGui.Button("Copy error log path")) ImGui.SetClipboardText(errorJournal.FilePath);
            if (ImGui.Button("Export diagnostics with error history")) Export();
            if (exportPath is not null)
            {
                ImGui.TextWrapped(exportPath);
                if (ImGui.Button("Copy diagnostic export path")) ImGui.SetClipboardText(exportPath);
            }
            if (errorJournal.WriteFailure is { } failure) ImGui.TextWrapped(failure);
            ImGui.TextWrapped("Send the exported file when asking for help. Error logs exclude pairing keys, chat and raw server responses. The full diagnostic export also includes your recorded characters, house and garden details. Logs are not uploaded automatically.");
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }
    private void DrawSavedPairingKey()
    {
        if (config.PairingKey.Length == 0) return;
        ImGui.TextUnformatted("Saved pairing key");
        var savedKey = config.PairingKey;
        ImGui.InputText("##saved-pairing-key", ref savedKey, 128,
            ImGuiInputTextFlags.ReadOnly | (showSavedPairingKey ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password));
        if (ImGui.SmallButton(showSavedPairingKey ? "Hide key" : "Show key")) showSavedPairingKey = !showSavedPairingKey;
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy key")) ImGui.SetClipboardText(config.PairingKey);
        ImGui.TextWrapped("Share only with trusted people: this key reads the shared character/house list and allows sending game records to this Journal. It is not a view-only guest key.");
        ImGui.Separator();
    }
    private void DrawGeneralSettings()
    {
        ImGui.TextWrapped("Tracking and enabled uploads continue when the plugin window is closed.");
        var notify = config.NotifyHouseEntries;
        if (ImGui.Checkbox("Show house-entry messages in my chat",ref notify))
        { config.NotifyHouseEntries=notify;Pi.SavePluginConfig(config); }
        ImGui.TextDisabled("Only visible to you. Never sends to FC, party, tell or public chat.");
        ImGui.TextWrapped("Example: [Equinox] You entered your FC House — HAVEN28-45. Only new interior entries trigger a message; login observations and old sync records do not.");
        var background = config.RefreshSharedInBackground;
        if (ImGui.Checkbox("Refresh shared profiles while this window is closed",ref background))
        { config.RefreshSharedInBackground=background;nextRosterRead=default;Pi.SavePluginConfig(config); }
        ImGui.TextWrapped("One shared-list check per minute while open, or also in the background if enabled. Game actions still upload only when there are pending records. Both plugins can use the same pairing key.");
    }
}
