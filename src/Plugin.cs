using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Hooking;
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
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static ICondition Conditions { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle Addons { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private static readonly string[] GardenMenus = ["HousingGardening", "SelectString", "SelectIconString", "ContextMenu", "SelectYesno"];
    private static readonly AddonEvent[] MenuEvents = [AddonEvent.PostSetup, AddonEvent.PostRefresh, AddonEvent.PreReceiveEvent, AddonEvent.PreFinalize];
    private int menuObservations;
    private GardenMenu? activeGardenMenu;
    private TendIntent? pendingTend;
    private Address? currentAddress;
    private bool ObservingGardens => !faulted && (recording || config.TrackGardens);
    private unsafe delegate byte FireCallbackDelegate(AtkUnitBase* addon, uint count, AtkValue* values, byte close);
    private Hook<FireCallbackDelegate>? callbackHook;
    private string callbackStatus = "Not initialized";
    private string lastSubmittedOption = "None recorded";
    private readonly Configuration config;
    private readonly ObservationGate gate = new();
    private readonly List<Diagnostic> diagnostics = [];
    private readonly ConcurrentQueue<Diagnostic> menuMessages = new();
    private readonly ConcurrentQueue<(DateTimeOffset At, uint Id, int?[] Parameters, GardenContext? Context, TendIntent? Intent)> messages = new();
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
    private readonly CompanionSync sync = new();
    private Task<SyncResult>? syncTask;
    private DateTimeOffset nextSync;
    private string syncStatus = "Not paired. Local records only.";
    private string pairingInput = "";
    private int syncFailures;

    private void UpdateSync(DateTimeOffset now)
    {
        if (syncTask?.IsCompleted == true)
        {
            if (syncTask.IsCompletedSuccessfully)
            {
                var result = syncTask.Result;
                var ids = config.SentEvents.ToHashSet();
                foreach (var id in result.Accepted) if (ids.Add(id)) config.SentEvents.Add(id);
                var retained = config.Houses.Select(h => h.EventId).Concat(config.Tending.Select(t => t.EventId)).ToHashSet();
                config.SentEvents.RemoveAll(id => !retained.Contains(id));
                Pi.SavePluginConfig(config);
                syncStatus = result.Status;
                syncFailures = result.Retry ? Math.Min(syncFailures + 1, 5) : 0;
                nextSync = now.AddSeconds(result.Retry ? Math.Min(600, 30 * (1 << syncFailures)) : 30);
            }
            else { syncStatus = "Sync paused after a connection error; local records are kept."; nextSync = now.AddMinutes(2); }
            syncTask = null;
        }
        if (!config.SyncEnabled || config.PairingKey.Length != 64 || syncTask is not null || now < nextSync) return;
        var sent = config.SentEvents.ToHashSet();
        var events = config.Houses.Where(h => h.Kind == "house.entered" && !sent.Contains(h.EventId))
            .Select(h => new SyncEvent(h.EventId, h.Kind, h.ObservedAt, WithWorldNames(h.Actor), WithAddressNames(h.Address)))
            .Concat(config.Tending.Where(t => !sent.Contains(t.EventId)).Select(t => new SyncEvent(t.EventId, "garden.tended", t.ConfirmedAt, WithWorldNames(t.Actor), WithAddressNames(t.Address), t.Patch, t.Bed)))
            .OrderBy(e => e.At).Take(50).ToArray();
        if (events.Length == 0) { nextSync = now.AddSeconds(30); return; }
        syncStatus = $"Sending {events.Length} events…";
        syncTask = sync.Send(config.PairingKey, events);
    }

    public unsafe Plugin()
    {
        try
        {
            callbackHook = Interop.HookFromAddress<FireCallbackDelegate>(
                AtkUnitBase.MemberFunctionPointers.FireCallback, ObserveCallback);
            callbackStatus = "Ready";
        }
        catch (Exception ex) { callbackStatus = "Unavailable; see /xllog"; Log.Error(ex, "Garden callback observer unavailable"); }
        config = Pi.GetPluginConfig() as Configuration ?? new();
        if (config.TrackGardens) callbackHook?.Enable();
        Commands.AddHandler("/equinox", new CommandInfo(OnCommand) { HelpMessage = "Open Equinox Companion test recorder." });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
        Framework.Update += Update;
        Chat.LogMessage += OnLog;
        foreach (var menuEvent in MenuEvents) Addons.RegisterListener(menuEvent, GardenMenus, OnGardenMenu);
    }

    private void Open() => visible = true;
    private void OnCommand(string command, string args) => visible = !visible;

    private Actor ReadActor() => new(Player.ContentId.ToString(CultureInfo.InvariantCulture),
        Player.CharacterName, Player.HomeWorld.RowId, Player.CurrentWorld.RowId, WorldName(Player.HomeWorld.RowId), WorldName(Player.CurrentWorld.RowId));

    private static string? WorldName(uint id) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(id)?.Name.ToString();
    private static Actor WithWorldNames(Actor actor) => actor with { HomeWorldName = WorldName(actor.HomeWorldId), CurrentWorldName = WorldName(actor.CurrentWorldId) };

    private static string? DistrictName(uint id) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(id)?.PlaceName.Value.Name.ToString();
    private static Address WithAddressNames(Address address) => address with { WorldName = WorldName(address.WorldId), DistrictName = DistrictName(address.TerritoryTypeId) };

    private static Address? AddressOf(HouseId id)
    {
        if (id.Id == 0 || id.Id == ulong.MaxValue || id.WorldId == 0 || id.TerritoryTypeId == 0) return null;
        if (id.WardIndex >= 60 || (!id.IsApartment && id.PlotIndex >= 60)) return null;
        return new(id.Id.ToString("X16"), id.WorldId, id.TerritoryTypeId,
            id.WardIndex + 1, id.IsApartment ? 0 : id.PlotIndex + 1,
            id.RoomNumber, id.IsApartment, id.IsWorkshop, WorldName(id.WorldId), DistrictName(id.TerritoryTypeId));
    }

    private unsafe void Update(IFramework framework)
    {
        var now = DateTimeOffset.UtcNow;
        UpdateSync(now);
        if (faulted) return;
        
        if (!Player.IsLoaded || Player.ContentId == 0)
        {
            character = 0; currentAddress = null; gate.Reset(); snapshot = null; StopRecording();
            status = "Waiting for your character."; return;
        }
        if (character != Player.ContentId)
        {
            gate.Reset(); StopRecording(); character = Player.ContentId;
        }
        if (recording && now >= recordingUntil) StopRecording();
        if (Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51])
        {
            snapshot = null; currentAddress = null; Volatile.Write(ref pendingTend, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null); messages.Clear(); menuMessages.Clear(); status = "Waiting for the area to finish loading."; return;
        }
        if (now < nextSample) return;
        nextSample = now.AddMilliseconds(ObservingGardens ? 50 : 250);
        try
        {
            var manager = HousingManager.Instance();
            if (manager == null || manager->CurrentTerritory == null)
            {
                // Ordinary non-housing zone. A transient load must not create a visit.
                snapshot = null; currentAddress = null; Volatile.Write(ref pendingTend, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null);
                if (Client.TerritoryType != 0) gate.Observe("outside", now);
                status = "No housing territory loaded.";
                DrainMessages();
                return;
            }
            if (!manager->CurrentTerritory->IsLoaded()) { Volatile.Write(ref pendingTend, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null); messages.Clear(); menuMessages.Clear(); return; }
            var type = manager->GetCurrentHousingTerritoryType();
            var inside = type == HousingTerritoryType.Indoor;
            var address = AddressOf(inside ? manager->GetCurrentIndoorHouseId() : manager->GetCurrentHouseId());
            currentAddress = address;
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

            if (!ObservingGardens) { snapshot = null; messages.Clear(); menuMessages.Clear(); return; }

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
        // Copy only garden-associated menus. Never modify or dispatch a UI event.
        if (!ObservingGardens || menuMessages.Count >= 128 ||
            !Player.IsLoaded || Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51]) return;
        var now = DateTimeOffset.UtcNow;
        var candidate = Volatile.Read(ref capturedContext)?.CandidateAt(now);
        if (candidate?.TargetDetails?.DataId != 2003757 || candidate.Actor.ContentId != Player.ContentId.ToString(CultureInfo.InvariantCulture)) return;
        var received = args as AddonReceiveEventArgs;
        var eventType = received is null ? (AtkEventType?)null : (AtkEventType)received.AtkEventType;
        // Hover/motion noise isn't evidence of an action.
        if (eventType is AtkEventType.MouseMove or AtkEventType.MouseOver or AtkEventType.MouseOut
            or AtkEventType.ListItemRollOver or AtkEventType.ListItemRollOut) return;
        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (addon == null) return;
            if (args.AddonName == "SelectString")
            {
                if (type == AddonEvent.PreFinalize) Volatile.Write(ref activeGardenMenu, null);
                else if (type is AddonEvent.PostSetup or AddonEvent.PostRefresh)
                {
                    Volatile.Write(ref activeGardenMenu, null);
                    if (addon->AtkValues != null && addon->AtkValuesCount > 7 &&
                        ((int)addon->AtkValues[5].Type & 15) == 3)
                    {
                        var count = addon->AtkValues[5].Int;
                        var titleType = (int)addon->AtkValues[2].Type & 15;
                        if (count is > 0 and <= 16 && 7 + count <= addon->AtkValuesCount && titleType is 8 or 10)
                        {
                            var title = CopyMenuText(addon->AtkValues[2].String.Value) ?? "";
                            var options = new string[count];
                            var valid = true;
                            for (var j = 0; j < count; j++)
                            {
                                var optionType = (int)addon->AtkValues[7 + j].Type & 15;
                                if (optionType is not (8 or 10)) { valid = false; break; }
                                options[j] = CopyMenuText(addon->AtkValues[7 + j].String.Value) ?? "";
                            }
                            if (valid) Volatile.Write(ref activeGardenMenu,
                                new((nint)addon, now, candidate, title, options));
                        }
                    }
                }
            }
            int? index = null;
            if (received is not null && received.AtkEventData != 0 &&
                eventType is AtkEventType.ListItemClick or AtkEventType.ListItemSelect)
                index = ((AtkEventData*)received.AtkEventData)->ListItemData.SelectedIndex;
            var values = new List<object>();
            if (addon->AtkValues != null && type != AddonEvent.PreFinalize)
                for (var i = 0; i < Math.Min((int)addon->AtkValuesCount, 64); i++)
                {
                    var value = addon->AtkValues[i];
                    var valueType = (int)value.Type & 15;
                    object? copied = valueType switch {
                        2 => value.Bool, 3 => value.Int, 5 => value.UInt,
                        8 or 10 => CopyMenuText(value.String.Value), _ => null
                    };
                    if (copied is not null) values.Add(new { index = i, type = value.Type.ToString(), value = copied });
                }
            menuMessages.Enqueue(new(now, "garden.menuObservation", new {
                addon = args.AddonName, lifecycle = type.ToString(),
                eventType = eventType?.ToString(), eventParam = received?.EventParam,
                selectedIndexCandidate = index, values, candidateTarget = candidate,
                confirmedAction = false, confirmedBed = false
            }));
        }
        catch (Exception ex) { Log.Error(ex, "Could not copy garden menu diagnostic"); }
    }

    private static unsafe string? CopyMenuText(byte* value)
    {
        if (value == null) return null;
        var length = 0;
        while (length < 512 && value[length] != 0) length++;
        return System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(value, length));
    }

    private unsafe byte ObserveCallback(AtkUnitBase* addon, uint count, AtkValue* values, byte close)
    {
        // Forward the original callback exactly once with untouched arguments and return value.
        // No game action is initiated here. Only an already-open garden menu is observed.
        try
        {
            var menu = Volatile.Read(ref activeGardenMenu);
            var now = DateTimeOffset.UtcNow;
            if (ObservingGardens && menu is not null && menu.AddonAddress == (nint)addon &&
                count is > 0 and <= 16 && values != null && menuMessages.Count < 128 &&
                Player.IsLoaded && menu.Target.Actor.ContentId == Player.ContentId.ToString(CultureInfo.InvariantCulture) &&
                !Conditions[ConditionFlag.BetweenAreas] && !Conditions[ConditionFlag.BetweenAreas51] &&
                menu.Matches(now, Volatile.Read(ref capturedContext)?.CandidateAt(now)))
            {
                var copied = new int?[(int)count];
                for (var i = 0; i < count; i++)
                {
                    var valueType = (int)values[i].Type & 15;
                    if (valueType == 3) copied[i] = values[i].Int;
                    else if (valueType == 5 && values[i].UInt <= int.MaxValue) copied[i] = (int)values[i].UInt;
                }
                var option = menu.OptionAt(copied[0]);
                Volatile.Write(ref pendingTend, TendIntent.From(menu, option, now));
                menuMessages.Enqueue(new(now, "garden.callbackObservation", new {
                    menuTitle = menu.Title, options = menu.Options, arguments = copied,
                    selectedOptionCandidate = option, close = close != 0,
                    candidateTarget = menu.Target, confirmedAction = false,
                    note = "Submitted option is not proof of successful execution."
                }));
                lastSubmittedOption = $"{menu.Title}: {option ?? "unresolved (see export)"}";
            }
        }
        catch (Exception ex) { Log.Error(ex, "Could not copy garden callback diagnostic"); }
        return callbackHook!.Original(addon, count, values, close);
    }

    private void OnLog(ILogMessage message)
    {
        // No chat text, string parameters, or game writes. Copy only numeric values
        // while the native message is valid; inspect housing later on the framework thread.
        if (!ObservingGardens || messages.Count >= 256) return;
        var count = Math.Min((int)message.ParameterCount, 16);
        var parameters = new int?[count];
        for (var i = 0; i < count; i++) if (message.TryGetIntParameter(i, out var n)) parameters[i] = n;
        messages.Enqueue((DateTimeOffset.UtcNow, message.LogMessageId, parameters, Volatile.Read(ref capturedContext), Volatile.Read(ref pendingTend)));
    }

    private void DrainMessages()
    {
        while (menuMessages.TryDequeue(out var menu)) if (recording) { menuObservations++; AddDiagnostic(menu); }
        while (messages.TryDequeue(out var message))
        {
            if (!ObservingGardens) continue;
            var candidate = message.Context?.CandidateAt(message.At);
            var confirmed = message.Intent?.Confirm(message.Id, message.At, candidate);
            if (confirmed is not null && !config.Tending.Any(x => x.EventId == confirmed.EventId))
            {
                config.Tending.Add(confirmed);
                if (config.Tending.Count > 10000) config.Tending.RemoveRange(0, config.Tending.Count - 10000);
                Pi.SavePluginConfig(config);
            }
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
        if (!recording) return;
        if (diagnostics.Count >= 2000) { StopRecording(); status = "Recording limit reached. Export the test."; return; }
        diagnostics.Add(item);
    }

    private void StartRecording()
    {
        diagnostics.Clear(); recentSignals.Clear(); menuObservations = 0; messages.Clear(); menuMessages.Clear(); lastSnapshotKey = "";
        Volatile.Write(ref pendingTend, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null); snapshot = null; nextSample = default;
        lastSubmittedOption = "None recorded";
        callbackHook?.Enable();
        recordingUntil = DateTimeOffset.UtcNow.AddMinutes(5); recording = true; exportPath = null;
    }

    private void StopRecording() { recording = false; if (!ObservingGardens) callbackHook?.Disable(); Volatile.Write(ref pendingTend, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null); messages.Clear(); menuMessages.Clear(); lastSnapshotKey = ""; }

    private void Export()
    {
        try
        {
            var dir = Path.Combine(Pi.GetPluginConfigDirectory(), "exports");
            Directory.CreateDirectory(dir);
            exportPath = Path.Combine(dir, $"equinox-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            File.WriteAllText(exportPath, JsonSerializer.Serialize(new {
                schemaVersion = 3, pluginVersion = "0.3.0.0", exportedAt = DateTimeOffset.UtcNow,
                mode = "local-diagnostics", gardeningConfirmed = false,
                houseObservations = config.Houses, confirmedTending = config.Tending, diagnostics
            }, json));
        }
        catch (Exception ex) { exportPath = null; status = "Export failed; see /xllog."; Log.Error(ex, "Equinox export failed"); }
    }

    private void Draw()
    {
        if (!visible) return;
        ImGui.SetNextWindowSize(new Vector2(660, 480), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Equinox Companion", ref visible))
        {
            ImGui.TextWrapped("Local tracking version 0.3.0.0 — optional website connection available.");
            if (ImGui.CollapsingHeader("Website connection"))
            {
                ImGui.TextWrapped("First deploy Journal V7.9.14, then open Game connection on the website and create a pairing key.");
                ImGui.InputText("Pairing key", ref pairingInput, 128, ImGuiInputTextFlags.Password);
                if (ImGui.Button("Save pairing key") && syncTask is null)
                {
                    var key = pairingInput.Trim();
                    if (key.Length == 64 && key.All(c => char.IsAsciiHexDigit(c)))
                    { config.PairingKey = key.ToLowerInvariant(); pairingInput = ""; config.SentEvents.Clear(); nextSync = default; Pi.SavePluginConfig(config); syncStatus = "Paired. Enable sync to send saved entries and tending."; }
                    else syncStatus = "Paste the 64-character key from Game connection.";
                }
                var enabled = config.SyncEnabled;
                if (ImGui.Checkbox("Sync confirmed actions to Equinox Journal", ref enabled))
                { config.SyncEnabled = enabled; nextSync = default; Pi.SavePluginConfig(config); }
                ImGui.TextWrapped(syncStatus);
                ImGui.TextWrapped("Sends character identity, property address, entry and tending times. Pairing key is saved on this PC and is never included in test exports. Characters match automatically by name and home server. Unmatched houses and patches need linking once.");
            }
            ImGui.Separator(); ImGui.TextWrapped(status);
            if (ImGui.CollapsingHeader("Character house visits", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextWrapped("Touched means entered inside. Times are local. Houses listed here are observed visits, not an ownership roster.");
                if (currentAddress is not null && Player.IsLoaded && !config.Houses.Any(h => h.Actor.ContentId == Player.ContentId.ToString(CultureInfo.InvariantCulture) && h.Address.HouseId == currentAddress.HouseId))
                    ImGui.TextWrapped("Current property: no entry recorded for this character yet.");
                foreach (var house in config.Houses.GroupBy(h => (h.Actor.ContentId, h.Address.HouseId)).OrderByDescending(g => g.Max(h => h.ObservedAt)))
                {
                    var last = house.OrderByDescending(h => h.ObservedAt).First();
                    var entry = house.Where(h => h.Kind == "house.entered").OrderByDescending(h => h.ObservedAt).FirstOrDefault();
                    var a = WithAddressNames(last.Address);
                    ImGui.TextWrapped($"{last.Actor.Name} · {a.WorldName ?? a.WorldId.ToString()} · {a.DistrictName ?? a.TerritoryTypeId.ToString()} · W{a.Ward} P{a.Plot} · Room {a.Room}");
                    ImGui.TextWrapped(entry is null ? $"Observed inside {last.ObservedAt.ToLocalTime():g}; entry time unknown" : $"Touched: {entry.ObservedAt.ToLocalTime():g}");
                }
                ImGui.TextWrapped("No recorded entry means unknown. Entry observations do not confirm a demolition timer reset.");
            }
            ImGui.Separator();
            var track = config.TrackGardens;
            if (ImGui.Checkbox("Automatically save confirmed tending locally", ref track))
            {
                config.TrackGardens = track;
                if (ObservingGardens) callbackHook?.Enable(); else StopRecording();
                Pi.SavePluginConfig(config);
            }
            ImGui.TextWrapped("English garden menus supported. Confirmed tending is saved per house, patch and bed. Website matching preserves existing batches. Planting and harvest detection are upcoming.");
            ImGui.TextWrapped($"Saved tending records: {config.Tending.Count} (latest 10,000 retained)");
            foreach (var tend in config.Tending.TakeLast(6).Reverse())
                ImGui.TextWrapped($"{tend.ConfirmedAt.ToLocalTime():g} · {tend.Actor.Name} · W{tend.Address.Ward} P{tend.Address.Plot} · Patch {tend.Patch}, bed {tend.Bed}: tended");
            ImGui.Separator();
            ImGui.TextWrapped("Optional diagnostics: record your normal gardening routine, then export. Starting a test does not clear saved tending or house visits.");
            if (!recording)
            {
                if (ImGui.Button("Start a 5-minute garden test") && Player.IsLoaded && !faulted) StartRecording();
            }
            else
            {
                ImGui.Text($"Recording · {Math.Max(0, (int)(recordingUntil - DateTimeOffset.UtcNow).TotalSeconds)}s remaining");
                if (ImGui.Button("Stop recording")) StopRecording();
            }
            ImGui.Text($"Diagnostic records: {diagnostics.Count}/2000 · Menu observations: {menuObservations}");
            if (snapshot is not null)
            {
                ImGui.TextWrapped($"Target: {snapshot.TargetName ?? "none"} · furniture index: {snapshot.FurnitureIndex?.ToString() ?? "unknown"}");
                ImGui.TextWrapped($"Planting menu: {snapshot.PlantingMenuOpen} · item IDs: {string.Join(", ", snapshot.SelectedItemIds)}");
            }
            ImGui.TextWrapped($"Action observer: {callbackStatus}");
            ImGui.TextWrapped($"Last submitted option (unverified): {lastSubmittedOption}");
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
        sync.Dispose();
        StopRecording();
        callbackHook?.Disable();
        callbackHook?.Dispose();
        Chat.LogMessage -= OnLog;
        foreach (var menuEvent in MenuEvents) Addons.UnregisterListener(menuEvent, GardenMenus, OnGardenMenu);
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/equinox");
    }
}
