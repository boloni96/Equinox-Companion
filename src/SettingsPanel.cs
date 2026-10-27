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
            SettingsSection("Appearance",DrawAppearanceSettings);
            SettingsSection("Optional tools",DrawFeatureSettings);
            if(config.Seller.Enabled)SettingsSection("Market",()=>{if(ImGui.Button("Open Market & Seller settings"))OpenSellerSettings();SettingsHelp("One settings page for manual selling, automatic pricing, background checks and display.",false);});
            if(OptionalToolAvailable("Camera"))SettingsSection("Camera",DrawCameraHeightSettings);
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
        if(config.Seller.Enabled&&ImGui.CollapsingHeader("Market & Seller"))DrawSellerDiagnostics();
        if(config.EnableGilTrader&&ImGui.CollapsingHeader("Gil Trader"))DrawGilTradeDiagnostics();
        if((config.EnableTradeItemShortcut||tradeItemShortcutTrace.Count>0)&&ImGui.CollapsingHeader("Item trade shortcut"))
        {
            ImGui.TextWrapped(tradeItemShortcutStatus);
            ImGui.TextDisabled($"Trade selected: {tradeItemShortcutActions} · Items added to offer: {tradeItemShortcutConfirmed}");
            SettingsHelp("Added means observed in your trade offer, not handed to the other player. Final acceptance remains manual. The last 32 shortcut stages are included in Export diagnostics; no recording needs to be started.");
            foreach(var attempt in tradeItemShortcutTrace.TakeLast(8).Reverse())
            {
                ImGui.TextWrapped($"{attempt.At.ToLocalTime():HH:mm:ss} · {attempt.ItemName} · {attempt.Stage}");
                SettingsHelp($"Item {attempt.ItemId} · inventory {attempt.Inventory} · slot {attempt.Slot} · quantity {attempt.Quantity}\n{attempt.Detail}");
            }
        }
        if(ImGui.CollapsingHeader("Performance"))DrawRecentPerformanceDiagnostics();
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
        ImGui.TextUnformatted("Tools on this computer");
        MessageToggle("Ctrl + right-click items into trade", config.EnableTradeItemShortcut, SetTradeItemShortcutEnabled);
        SettingsHelp("With a trade already open, hold Ctrl and right-click an item in your inventory, Armoury Chest or any other container with an enabled Trade action. Stacks keep the game's quantity prompt. The item menu closes after adding the item or dismissing that prompt. You still review and accept the trade. Does nothing outside a trade or while automatic Gil Trader is running. This shortcut is independent of Enable Gil Trader.\n\n" + tradeItemShortcutStatus);
        MessageToggle("Enable Gil Trader", config.EnableGilTrader, SetGilTraderEnabled);
        SettingsHelp("Shows Gil Trader in the window menu and enables /eqtrade. Send a chosen gil total or receive from a selected nearby player. Disabling closes the window and stops the session; confirmed trades are kept.");
        MessageToggle("Enable Equinox Market & Seller",config.Seller.Enabled,SetMarketEnabled);
        SettingsHelp("Shows Market and Seller in the window menu and enables their commands. Seller opens at a summoning bell only when this and Open Seller at summoning bells are checked. Opening a bell never starts price changes. Disabling stops Seller, closes both windows and removes their icons. Settings and saved market records are kept; paired market sharing has its own setting.");
        SettingsHelp("Choose which tools are available on this computer. Disabled tools stay out of menus and cannot be opened by commands. Core character, house and garden tracking is controlled separately under Tracking.",false);
        MessageToggle("Enable QuickLoot",config.EnableQuickLoot,v=>{
            config.EnableQuickLoot=v;
            if(!v){config.QuickLoot.Automatic=false;StopQuickLoot("QuickLoot disabled.");quickLootBarEntry?.Remove();quickLootBarEntry=null;}
            HideDisabledOptionalWindows();
        });
        SettingsHelp("Disabled by default. Enable to show the QuickLoot tab and its loot settings. Disabling hides the tab, stops rolling and removes its top-bar entry; your rules are kept.");
        ImGui.Separator();
        MessageToggle("Enable Treasure Coffer markers", config.EnableCofferMarkers, SetCofferMarkers);
        SettingsHelp("Disabled on installation. Red/green coffer icons on the minimap and main map; local to this visit.");
        if(config.EnableCofferMarkers&&ImGui.CollapsingHeader("Coffer appearance & automatic opening")) DrawCofferSettings();
        ImGui.Separator();
        MessageToggle("Enable Equinox Helper", config.EnableFollowThem, v => {
            config.EnableFollowThem = v;
            if (!v) StopFollowThem("FollowThem disabled.");
            HideDisabledOptionalWindows(); RefreshFollowBar();
        });
        SettingsHelp("Disabled on installation. Enables the Helper tab (FollowThem and optional Quest Helper) and clickable top-bar entry. Settings stay on this PC and are never paired. Disabling stops FollowThem; your selected character and portal settings are kept.");
        ImGui.Separator();
        MessageToggle("Enable camera tools", OptionalToolAvailable("Camera"), SetCameraToolsEnabled);
        SettingsHelp("Shows Camera settings and the /cam keybind. /cam can then toggle saved/game height. Disabling restores game height and disables the command and keybind; your saved height and shortcut are kept.");
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



