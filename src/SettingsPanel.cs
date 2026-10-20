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
            if (message is not null) PrintEquinox(new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddUiForeground(45).AddText(message).AddUiForegroundOff().Build());
            pendingHouseNotices.Remove(visit);
        }
    }
    private static void SettingsHelp(string text,bool inline=true)
    {
        if(inline)ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if(ImGui.IsItemHovered()){ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*28);ImGui.TextUnformatted(text);ImGui.PopTextWrapPos();ImGui.EndTooltip();}
    }
    private void SettingsSection(string title,Action draw)
    {
        if(!ImGui.BeginTabItem(title))return;
        if(ImGui.BeginChild("SettingsBody##"+title,new System.Numerics.Vector2(0,0),false))draw();
        ImGui.EndChild();ImGui.EndTabItem();
    }
    private void DrawSettings()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new System.Numerics.Vector2(6,4));
        if(ImGui.BeginTabBar("SettingsSections",ImGuiTabBarFlags.FittingPolicyScroll))
        {
            SettingsSection("Optional tools",DrawFeatureSettings);
            SettingsSection("Camera",DrawCameraHeightSettings);
            SettingsSection("Tracking",DrawGeneralSettings);
            SettingsSection("Chat",DrawChatMessageSettings);
            SettingsSection("Keybinds",DrawShortcutSettings);
            SettingsSection("Characters",DrawCharacterOrderSettings);
            SettingsSection("Windows",DrawFloatingLauncherSettings);
            SettingsSection("Connection",DrawConnection);
            SettingsSection("Diagnostics",DrawCompactDiagnostics);
            ImGui.EndTabBar();
        }
        ImGui.PopStyleVar();
    }
    private string diagnosticFolderError="";
    private void OpenDiagnosticFolder(string path)
    {
        try{System.IO.Directory.CreateDirectory(path);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});diagnosticFolderError="";}
        catch(Exception ex){diagnosticFolderError="Could not open folder: "+ex.Message;}
    }
    private void DrawCompactDiagnostics()
    {
        PollDiagnosticExport();
        ImGui.BeginDisabled(DiagnosticExportBusy);
        if(ImGui.Button(DiagnosticExportBusy?"Exporting…":"Export diagnostics"))Export();
        ImGui.EndDisabled();
        SettingsHelp("Exports recorded characters, houses, gardens and error history to a local file. Pairing keys are excluded. Send this file when asking for help; it is not uploaded automatically.");
        if(ImGui.SmallButton("Open export folder"))OpenDiagnosticFolder(System.IO.Path.Combine(Pi.GetPluginConfigDirectory(),"exports"));
        ImGui.SameLine();if(ImGui.SmallButton("Open logs folder"))OpenDiagnosticFolder(System.IO.Path.GetDirectoryName(errorJournal.FilePath)??Pi.GetPluginConfigDirectory());
        if(diagnosticFolderError.Length>0)ImGui.TextWrapped(diagnosticFolderError);
        if(DiagnosticExportBusy)ImGui.TextDisabled("Writing diagnostics in the background…");
        if(diagnosticExportError.Length>0)ImGui.TextWrapped(diagnosticExportError);
        if(exportPath is not null){ImGui.TextDisabled("Export ready");SettingsHelp(exportPath);}
        if(ImGui.CollapsingHeader("File paths")){
            if(ImGui.SmallButton("Copy error log path"))ImGui.SetClipboardText(errorJournal.FilePath);SettingsHelp(errorJournal.FilePath);
            if(exportPath is not null){if(ImGui.SmallButton("Copy export path"))ImGui.SetClipboardText(exportPath);SettingsHelp(exportPath);}
        }
        if(errorJournal.WriteFailure is {} failure)ImGui.TextWrapped(failure);
        ImGui.TextDisabled($"Queued: {pendingSyncRecords} · ready: {readySyncRecords} · held: {heldSyncRecords}");
        SettingsHelp("Held records are incomplete observations. They do not block valid records. Logs are kept locally; repeated issues are limited to once every five minutes.");
        if(optionalToolFailures.Count>0&&ImGui.CollapsingHeader("Optional tools need attention")){foreach(var issue in optionalToolFailures)ImGui.TextWrapped(issue.Key+": "+issue.Value);}
        if(ImGui.CollapsingHeader("Performance"))DrawPerformanceDiagnostics();
        if(ImGui.CollapsingHeader("Tracking & recording"))DrawDiagnosticsTracking();
        if(ImGui.CollapsingHeader("Housing lottery"))ImGui.TextWrapped(lotteryDiagnostic is null?"No lottery observation this session.":System.Text.Json.JsonSerializer.Serialize(lotteryDiagnostic));
        if(ImGui.CollapsingHeader("Garden artwork preview"))DrawGardenArtworkPreview();
        if(ImGui.SmallButton("About / What's new"))welcomeWindow.IsOpen=true;
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
        SettingsHelp("Share only with trusted people: this key reads the shared character/house list and allows sending game records to this Journal. It is not a view-only guest key.");
        ImGui.Separator();
    }
    private void DrawFeatureSettings()
    {
        ImGui.TextUnformatted("Optional features");
        MessageToggle("Enable QuickLoot",config.EnableQuickLoot,v=>{
            config.EnableQuickLoot=v;
            if(!v){config.QuickLoot.Automatic=false;StopQuickLoot("QuickLoot disabled.");quickLootBarEntry?.Remove();quickLootBarEntry=null;}
        });
        SettingsHelp("Disabled by default. Enable to show the QuickLoot tab and its loot settings. Disabling hides the tab, stops rolling and removes its top-bar entry; your rules are kept.");
        ImGui.Separator();
        MessageToggle("Enable Treasure Coffer markers", config.EnableCofferMarkers, SetCofferMarkers);
        SettingsHelp("Disabled on installation. Red/green coffer icons on the minimap and main map; local to this visit.");
        if(config.EnableCofferMarkers&&ImGui.CollapsingHeader("Coffer appearance & automatic opening")) DrawCofferSettings();
        ImGui.Separator();
        MessageToggle("Enable Equinox Helper", config.EnableFollowThem, v => {
            if (!v) StopFollowThem("FollowThem disabled.");
            config.EnableFollowThem = v; RefreshFollowBar();
        });
        SettingsHelp("Disabled on installation. Enables the Helper tab (FollowThem and optional Quest Helper) and clickable top-bar entry. Settings stay on this PC and are never paired. Disabling stops FollowThem; your selected character and portal settings are kept.");
    }
    private void DrawGeneralSettings()
    {
        ImGui.TextUnformatted("Website sync");
        MessageToggle("Sync confirmed actions to Equinox Journal",config.SyncEnabled,v=>{config.SyncEnabled=v;nextSync=default;});
        MessageToggle("Refresh shared profiles while this window is closed",config.RefreshSharedInBackground,v=>{config.RefreshSharedInBackground=v;nextRosterRead=default;});
        SettingsHelp("Shared profiles refresh every 15 seconds. Actions upload when records are pending. Pairing keys are managed under Connection.");
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
        SettingsHelp("Tracking continues while the window is closed. Garden records include confirmed seed, soil and care actions. Chat reminders are separate, under Chat messages.");
        ImGui.Separator();ImGui.TextUnformatted("Collection status");
        ImGui.TextWrapped(collectionStatus);
        SettingsHelp("Open the Armoire, Glamour Dresser and each retainer to refresh their stored contents. Current state and unsent records are kept; older acknowledged history is pruned after 60 days.");
    }
}

