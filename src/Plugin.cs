using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NativeEventObject = FFXIVClientStructs.FFXIV.Client.Game.Object.EventObject;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace EquinoxCompanion;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static ICondition Conditions { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle Addons { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration config;
    private readonly ObservationGate gate = new();
    private readonly List<Diagnostic> diagnostics = [];
    private readonly ConcurrentQueue<Diagnostic> menuMessages = new();
    private readonly ConcurrentQueue<(DateTimeOffset At, uint Id, int?[] Parameters, GardenContext? Context)> messages = new();
    private readonly JsonSerializerOptions json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private bool visible = true;
    private volatile bool recording;
    private DateTimeOffset recordingUntil;
    private DateTimeOffset nextSample;
    private ulong character;
    private GardenSnapshot? snapshot;
    private GardenContext? capturedContext;
    private readonly List<string> recentSignals = [];
    private string lastSnapshotKey = "";
    private string status = "Waiting for your character.";
    private string? exportPath;
    private bool faulted;

    public Plugin()
    {
        config = Pi.GetPluginConfig() as Configuration ?? new();
        Commands.AddHandler("/equinox", new CommandInfo(OnCommand) { HelpMessage = "Open Equinox Companion test recorder." });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
        Framework.Update += Update;
        Chat.LogMessage += OnLog;
        Addons.RegisterListener(AddonEvent.PreReceiveEvent, "SelectString", OnGardenMenu);
    }

    private void Open() => visible = true;
    private void OnCommand(string command, string args) => visible = !visible;

    private Actor ReadActor() => new(Player.ContentId.ToString(CultureInfo.InvariantCulture),
        Player.CharacterName, Player.HomeWorld.RowId, Player.CurrentWorld.RowId);

    private static Address? AddressOf(HouseId id)
    {
        if (id.Id == 0 || id.Id == ulong.MaxValue || id.WorldId == 0 || id.TerritoryTypeId == 0) return null;
        if (id.WardIndex >= 60 || (!id.IsApartment && id.PlotIndex >= 60)) return null;
        return new(id.Id.ToString("X16"), id.WorldId, id.TerritoryTypeId,
            id.WardIndex + 1, id.IsApartment ? 0 : id.PlotIndex + 1,
            id.RoomNumber, id.IsApartment, id.IsWorkshop);
    }

    private unsafe void Update(IFramework framework)
    {
        if (faulted) return;
        var now = DateTimeOffset.UtcNow;
        
        if (!Player.IsLoaded || Player.ContentId == 0)
        {
            character = 0; gate.Reset(); snapshot = null; StopRecording();
            status = "Waiting for your character."; return;
        }
        if (character != Player.ContentId)
        {
            gate.Reset(); StopRecording(); character = Player.ContentId;
        }
        if (recording && now >= recordingUntil) StopRecording();
        if (Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51])
        {
            snapshot = null; Volatile.Write(ref capturedContext, null); messages.Clear(); menuMessages.Clear(); status = "Waiting for the area to finish loading."; return;
        }
        if (now < nextSample) return;
        nextSample = now.AddMilliseconds(recording ? 50 : 250);
        try
        {
            var manager = HousingManager.Instance();
            if (manager == null || manager->CurrentTerritory == null)
            {
                // Ordinary non-housing zone. A transient load must not create a visit.
                snapshot = null; Volatile.Write(ref capturedContext, null);
                if (Client.TerritoryType != 0) gate.Observe("outside", now);
                status = "No housing territory loaded.";
                DrainMessages();
                return;
            }
            if (!manager->CurrentTerritory->IsLoaded()) { Volatile.Write(ref capturedContext, null); messages.Clear(); menuMessages.Clear(); return; }
            var type = manager->GetCurrentHousingTerritoryType();
            var inside = type == HousingTerritoryType.Indoor;
            var address = AddressOf(inside ? manager->GetCurrentIndoorHouseId() : manager->GetCurrentHouseId());
            var actor = ReadActor();
            if (inside && address is not null)
            {
                var kind = gate.Observe(address.HouseId, now);
                if (kind is not null)
                {
                    config.Houses.Add(new(Guid.NewGuid().ToString("N"), now, kind, actor, address));
                    if (config.Houses.Count > 500) config.Houses.RemoveRange(0, config.Houses.Count - 500);
                    Pi.SavePluginConfig(config);
                }
            }
            else if (type == HousingTerritoryType.Outdoor) gate.Observe("outside", now);
            status = address is null ? "Housing loaded; no complete property address yet." :
                $"World {address.WorldId} · Territory {address.TerritoryTypeId} · Ward {address.Ward} · Plot {address.Plot}";

            if (!recording) { snapshot = null; messages.Clear(); menuMessages.Clear(); return; }

            // This is candidate context, never a confirmed bed or successful action.
            uint? objectId = null; short? furnitureIndex = null;
            if (manager->OutdoorTerritory != null && type == HousingTerritoryType.Outdoor)
            {
                var obj = manager->OutdoorTerritory->TargetedHousingObject;
                if (obj != null) { objectId = obj->HousingObjectId.Id; furnitureIndex = obj->HousingFurnitureIndex; }
            }
            var target = Targets.Target;
            uint? eventArgument = null; ushort? timelineState = null;
            if (target is not null && target.ObjectKind == ObjectKind.EventObj && target.BaseId == 2003757 && target.Address != 0)
            {
                var eventObject = (NativeEventObject*)target.Address;
                eventArgument = eventObject->Arg;
                timelineState = eventObject->SharedTimelineState;
            }
            var plant = AgentHousingPlant.Instance();
            var planting = plant != null && plant->IsAgentActive();
            uint[] items = planting ? [plant->SelectedItems[0].ItemId, plant->SelectedItems[1].ItemId] : [];
            snapshot = new(now, actor, address, target?.GameObjectId.ToString("X16"),
                target?.Name.ToString(), objectId, furnitureIndex, planting, items,
                target is null ? null : new(target.BaseId, target.EntityId, target.ObjectKind.ToString(),
                    target.Position.X, target.Position.Y, target.Position.Z, eventArgument, timelineState));
            Volatile.Write(ref capturedContext, GardenContext.Capture(Volatile.Read(ref capturedContext), snapshot));
            if (recording)
            {
                var key = JsonSerializer.Serialize(new { address, snapshot.TargetId, snapshot.TargetDetails, objectId, furnitureIndex, planting, items });
                if (key != lastSnapshotKey) { lastSnapshotKey = key; AddDiagnostic(new(now, "garden.context", snapshot)); }
            }
            DrainMessages();
        }
        catch (Exception ex)
        {
            faulted = true; StopRecording();
            status = "Recorder paused after an error. See /xllog. Reload after checking game/plugin compatibility.";
            Log.Error(ex, "Equinox observation paused");
        }
    }

    private unsafe void OnGardenMenu(AddonEvent type, AddonArgs args)
    {
        // Observe the existing game menu; never modify or dispatch a UI event.
        if (!recording || faulted || menuMessages.Count >= 128 ||
            !Player.IsLoaded || Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51] ||
            args is not AddonReceiveEventArgs received || received.AtkEventData == 0) return;
        var eventType = (AtkEventType)received.AtkEventType;
        if (eventType is not (AtkEventType.ListItemClick or AtkEventType.ListItemSelect)) return;
        var now = DateTimeOffset.UtcNow;
        var candidate = Volatile.Read(ref capturedContext)?.CandidateAt(now);
        if (candidate?.TargetDetails?.DataId != 2003757 || candidate.Actor.ContentId != Player.ContentId.ToString(CultureInfo.InvariantCulture)) return;
        try
        {
            var addon = (AddonSelectString*)args.Addon.Address;
            if (addon == null) return;
            var index = ((AtkEventData*)received.AtkEventData)->ListItemData.SelectedIndex;
            var count = addon->PopupMenu.EntryCount;
            if (index < 0 || count < 1 || count > 32 || index >= count || addon->PopupMenu.EntryNames == null) return;
            var label = addon->PopupMenu.EntryNames[index].ToString();
            if (label.Length > 160) label = label[..160];
            menuMessages.Enqueue(new(now, "garden.menuSelectionCandidate", new {
                eventType = eventType.ToString(), eventParam = received.EventParam,
                selectedIndex = index, selectedLabel = label, candidateTarget = candidate,
                confirmedAction = false, confirmedBed = false
            }));
        }
        catch (Exception ex) { Log.Error(ex, "Could not copy garden menu diagnostic"); }
    }

    private void OnLog(ILogMessage message)
    {
        // No chat text, string parameters, or game writes. Copy only numeric values
        // while the native message is valid; inspect housing later on the framework thread.
        if (!recording || messages.Count >= 256) return;
        var count = Math.Min((int)message.ParameterCount, 16);
        var parameters = new int?[count];
        for (var i = 0; i < count; i++) if (message.TryGetIntParameter(i, out var n)) parameters[i] = n;
        messages.Enqueue((DateTimeOffset.UtcNow, message.LogMessageId, parameters, Volatile.Read(ref capturedContext)));
    }

    private void DrainMessages()
    {
        while (menuMessages.TryDequeue(out var menu)) if (recording) AddDiagnostic(menu);
        while (messages.TryDequeue(out var message))
        {
            if (!recording) continue;
            var candidate = message.Context?.CandidateAt(message.At);
            var signal = GardenSignals.Classify(message.Id);
            AddDiagnostic(new(message.At, "game.logObservation", new {
                logMessageId = message.Id, parameters = message.Parameters, signal,
                context = message.Context?.Current, candidateTarget = candidate,
                targetAssociation = candidate is null ? "unavailable" : "recent-context-only",
                confirmedBed = false, bedSlot = (int?)null
            }));
            if (signal != "unclassified")
            {
                recentSignals.Add($"{message.At:HH:mm:ss} · {signal} · target {candidate?.TargetId ?? "unknown"}");
                if (recentSignals.Count > 6) recentSignals.RemoveAt(0);
            }
        }
    }

    private void AddDiagnostic(Diagnostic item)
    {
        if (diagnostics.Count >= 2000) { StopRecording(); status = "Recording limit reached. Export the test."; return; }
        diagnostics.Add(item);
    }

    private void StartRecording()
    {
        diagnostics.Clear(); recentSignals.Clear(); messages.Clear(); menuMessages.Clear(); lastSnapshotKey = "";
        Volatile.Write(ref capturedContext, null); snapshot = null; nextSample = default;
        recordingUntil = DateTimeOffset.UtcNow.AddMinutes(5); recording = true; exportPath = null;
    }

    private void StopRecording() { recording = false; Volatile.Write(ref capturedContext, null); messages.Clear(); menuMessages.Clear(); lastSnapshotKey = ""; }

    private void Export()
    {
        try
        {
            var dir = Path.Combine(Pi.GetPluginConfigDirectory(), "exports");
            Directory.CreateDirectory(dir);
            exportPath = Path.Combine(dir, $"equinox-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            File.WriteAllText(exportPath, JsonSerializer.Serialize(new {
                schemaVersion = 2, pluginVersion = "0.1.2.0", exportedAt = DateTimeOffset.UtcNow,
                mode = "local-diagnostics", gardeningConfirmed = false,
                houseObservations = config.Houses, diagnostics
            }, json));
        }
        catch (Exception ex) { exportPath = null; status = "Export failed; see /xllog."; Log.Error(ex, "Equinox export failed"); }
    }

    private void Draw()
    {
        if (!visible) return;
        ImGui.SetNextWindowSize(new Vector2(660, 480), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Equinox Companion · garden test", ref visible))
        {
            ImGui.TextWrapped("Local test version 0.1.2.0 — website sync is not connected yet.");
            ImGui.Separator(); ImGui.TextWrapped(status);
            ImGui.TextWrapped($"House observations saved: {config.Houses.Count}");
            ImGui.TextWrapped("Opening the plugin inside a house records 'observed inside', not a new entry. No demolition reset is claimed.");
            if (config.Houses.Count > 0)
            {
                var last = config.Houses[^1];
                ImGui.TextWrapped($"Last: {last.Kind} · {last.Actor.Name} · {last.ObservedAt:u}");
            }
            ImGui.Separator();
            ImGui.TextWrapped("Gardening test: start recording, then plant, tend, fertilize or harvest normally. Game signals are labeled below. Target associations and bed numbers are still unverified. Export before starting another test.");
            if (!recording)
            {
                if (ImGui.Button("Start a 5-minute garden test") && Player.IsLoaded && !faulted) StartRecording();
            }
            else
            {
                ImGui.Text($"Recording · {Math.Max(0, (int)(recordingUntil - DateTimeOffset.UtcNow).TotalSeconds)}s remaining");
                if (ImGui.Button("Stop recording")) StopRecording();
            }
            ImGui.Text($"Diagnostic records: {diagnostics.Count}/2000");
            if (snapshot is not null)
            {
                ImGui.TextWrapped($"Target: {snapshot.TargetName ?? "none"} · furniture index: {snapshot.FurnitureIndex?.ToString() ?? "unknown"}");
                ImGui.TextWrapped($"Planting menu: {snapshot.PlantingMenuOpen} · item IDs: {string.Join(", ", snapshot.SelectedItemIds)}");
            }
            foreach (var signal in recentSignals) ImGui.TextWrapped(signal);
            if (ImGui.Button("Export test JSON")) Export();
            if (exportPath is not null)
            {
                ImGui.TextWrapped(exportPath);
                if (ImGui.Button("Copy export path")) ImGui.SetClipboardText(exportPath);
            }
            ImGui.Separator();
            ImGui.TextWrapped("Export includes character names/IDs and house addresses. Includes garden menu labels. No account credentials or player chat text. Nothing is sent online.");
        }
        ImGui.End();
    }

    public void Dispose()
    {
        StopRecording();
        Chat.LogMessage -= OnLog;
        Addons.UnregisterListener(AddonEvent.PreReceiveEvent, "SelectString", OnGardenMenu);
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/equinox");
    }
}
