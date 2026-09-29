using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
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
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration config;
    private readonly ObservationGate gate = new();
    private readonly List<Diagnostic> diagnostics = [];
    private readonly ConcurrentQueue<(DateTimeOffset At, uint Id, int?[] Parameters)> messages = new();
    private readonly JsonSerializerOptions json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private bool visible = true;
    private volatile bool recording;
    private DateTimeOffset recordingUntil;
    private DateTimeOffset nextSample;
    private ulong character;
    private GardenSnapshot? snapshot;
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
        if (now < nextSample) return;
        nextSample = now.AddMilliseconds(250);
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
            snapshot = null; messages.Clear(); status = "Waiting for the area to finish loading."; return;
        }
        try
        {
            var manager = HousingManager.Instance();
            if (manager == null || manager->CurrentTerritory == null)
            {
                // Ordinary non-housing zone. A transient load must not create a visit.
                snapshot = null;
                if (Client.TerritoryType != 0) gate.Observe("outside", now);
                status = "No housing territory loaded.";
                DrainMessages(now, null);
                return;
            }
            if (!manager->CurrentTerritory->IsLoaded()) return;
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

            if (!recording) { snapshot = null; messages.Clear(); return; }

            // This is candidate context, never a confirmed bed or successful action.
            uint? objectId = null; short? furnitureIndex = null;
            if (manager->OutdoorTerritory != null && type == HousingTerritoryType.Outdoor)
            {
                var obj = manager->OutdoorTerritory->TargetedHousingObject;
                if (obj != null) { objectId = obj->HousingObjectId.Id; furnitureIndex = obj->HousingFurnitureIndex; }
            }
            var target = Targets.Target;
            var plant = AgentHousingPlant.Instance();
            var planting = plant != null && plant->IsAgentActive();
            uint[] items = planting ? [plant->SelectedItems[0].ItemId, plant->SelectedItems[1].ItemId] : [];
            snapshot = new(now, actor, address, target?.GameObjectId.ToString("X16"),
                target?.Name.ToString(), objectId, furnitureIndex, planting, items);
            if (recording)
            {
                var key = JsonSerializer.Serialize(new { address, snapshot.TargetId, objectId, furnitureIndex, planting, items });
                if (key != lastSnapshotKey) { lastSnapshotKey = key; AddDiagnostic(new(now, "garden.context", snapshot)); }
            }
            DrainMessages(now, snapshot);
        }
        catch (Exception ex)
        {
            faulted = true; StopRecording();
            status = "Recorder paused after an error. See /xllog. Reload after checking game/plugin compatibility.";
            Log.Error(ex, "Equinox observation paused");
        }
    }

    private void OnLog(ILogMessage message)
    {
        // No chat text, string parameters, or game writes. Copy only numeric values
        // while the native message is valid; inspect housing later on the framework thread.
        if (!recording || messages.Count >= 256) return;
        var count = Math.Min((int)message.ParameterCount, 16);
        var parameters = new int?[count];
        for (var i = 0; i < count; i++) if (message.TryGetIntParameter(i, out var n)) parameters[i] = n;
        messages.Enqueue((DateTimeOffset.UtcNow, message.LogMessageId, parameters));
    }

    private void DrainMessages(DateTimeOffset now, GardenSnapshot? context)
    {
        while (messages.TryDequeue(out var message))
            if (recording && context is not null)
                AddDiagnostic(new(message.At, "game.logCandidate", new {
                    logMessageId = message.Id, parameters = message.Parameters,
                    contextReadAt = now, context,
                    confirmedAction = false, bedSlot = (int?)null
                }));
    }

    private void AddDiagnostic(Diagnostic item)
    {
        if (diagnostics.Count >= 2000) { StopRecording(); status = "Recording limit reached. Export the test."; return; }
        diagnostics.Add(item);
    }

    private void StartRecording()
    {
        diagnostics.Clear(); messages.Clear(); lastSnapshotKey = "";
        recordingUntil = DateTimeOffset.UtcNow.AddMinutes(5); recording = true; exportPath = null;
    }

    private void StopRecording() { recording = false; messages.Clear(); lastSnapshotKey = ""; }

    private void Export()
    {
        try
        {
            var dir = Path.Combine(Pi.GetPluginConfigDirectory(), "exports");
            Directory.CreateDirectory(dir);
            exportPath = Path.Combine(dir, $"equinox-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            File.WriteAllText(exportPath, JsonSerializer.Serialize(new {
                schemaVersion = 1, pluginVersion = "0.1.0", exportedAt = DateTimeOffset.UtcNow,
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
        if (ImGui.Begin("Equinox Companion · first test", ref visible))
        {
            ImGui.TextWrapped("Local test version 0.1.0 — website sync is not connected yet.");
            ImGui.Separator(); ImGui.TextWrapped(status);
            ImGui.TextWrapped($"House observations saved: {config.Houses.Count}");
            ImGui.TextWrapped("Opening the plugin inside a house records 'observed inside', not a new entry. No demolition reset is claimed.");
            if (config.Houses.Count > 0)
            {
                var last = config.Houses[^1];
                ImGui.TextWrapped($"Last: {last.Kind} · {last.Actor.Name} · {last.ObservedAt:u}");
            }
            ImGui.Separator();
            ImGui.TextWrapped("Gardening test: start recording, then plant, tend, fertilize or harvest normally. This records candidate identifiers, not confirmed actions or bed numbers.");
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
            if (ImGui.Button("Export test JSON")) Export();
            if (exportPath is not null)
            {
                ImGui.TextWrapped(exportPath);
                if (ImGui.Button("Copy export path")) ImGui.SetClipboardText(exportPath);
            }
            ImGui.Separator();
            ImGui.TextWrapped("Export includes character names/IDs and house addresses. No account credentials or player chat text. Nothing is sent online.");
        }
        ImGui.End();
    }

    public void Dispose()
    {
        StopRecording();
        Chat.LogMessage -= OnLog;
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/equinox");
    }
}
