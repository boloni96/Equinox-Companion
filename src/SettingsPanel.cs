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
        if (ImGui.BeginTabItem("Tracking"))
        {
            DrawGeneralSettings();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Chat messages")) { DrawChatMessageSettings(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Keybinds")) { DrawShortcutSettings(); ImGui.EndTabItem(); }
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
            DrawGardenArtworkPreview();
            if(ImGui.Button("About Equinox Companion / What’s new"))welcomeWindow.IsOpen=true;
            DrawDiagnosticsTracking();
            ImGui.Separator();ImGui.TextUnformatted("Logs and export");
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
        ImGui.TextUnformatted("Optional features");
        MessageToggle("Enable QuickLoot",config.EnableQuickLoot,v=>{
            config.EnableQuickLoot=v;
            if(!v){config.QuickLoot.Automatic=false;StopQuickLoot("QuickLoot disabled.");quickLootBarEntry?.Remove();quickLootBarEntry=null;}
        });
        ImGui.TextWrapped("Disabled by default. Enable to show the QuickLoot tab and its loot settings. Disabling hides the tab, stops rolling and removes its top-bar entry; your rules are kept.");
        ImGui.Separator();
        ImGui.TextUnformatted("Website sync");
        MessageToggle("Sync confirmed actions to Equinox Journal",config.SyncEnabled,v=>{config.SyncEnabled=v;nextSync=default;});
        MessageToggle("Refresh shared profiles while this window is closed",config.RefreshSharedInBackground,v=>{config.RefreshSharedInBackground=v;nextRosterRead=default;});
        ImGui.TextWrapped("Shared profiles refresh every 15 seconds. Actions upload when records are pending. Pairing keys are managed under Connection.");
        ImGui.Separator();ImGui.TextUnformatted("Information to collect");
        MessageToggle("Character and job details",config.SyncCharacterDetails,v=>{config.SyncCharacterDetails=v;nextSync=default;});
        MessageToggle("Private / FC houses and paired estate placards",config.SyncHouseDetails,v=>config.SyncHouseDetails=v);
        MessageToggle("Collection unlocks and reward items",config.SyncCollections,v=>config.SyncCollections=v);
        MessageToggle("Fashion Report and submarine observations",config.SyncActivities,v=>config.SyncActivities=v);
        MessageToggle("Import background submarine data and carried supplies",config.SyncAutoRetainer,v=>config.SyncAutoRetainer=v);
        ImGui.TextWrapped(autoRetainerStatus);
        MessageToggle("Garden planting and tending",config.TrackGardens,v=>{
            config.TrackGardens=v;
            if(ObservingGardens){callbackHook?.Enable();callbackIntHook?.Enable();plantHook?.Enable();}else StopRecording();
        });
        ImGui.TextWrapped("Tracking continues while the window is closed. Garden records include confirmed seed, soil and care actions. Chat reminders are separate, under Chat messages.");
        ImGui.Separator();ImGui.TextUnformatted("Collection status");
        ImGui.TextWrapped(collectionStatus);
        ImGui.TextWrapped("Open the Armoire, Glamour Dresser and each retainer to refresh their stored contents. Current state and unsent records are kept; older acknowledged history is pruned after 60 days.");
    }
}
