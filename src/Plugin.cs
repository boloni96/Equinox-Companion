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
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NativeEventObject = FFXIVClientStructs.FFXIV.Client.Game.Object.EventObject;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
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
    private PlantIntent? pendingPlant;
    private unsafe delegate void ConfirmPlantDelegate(AgentHousingPlant* agent);
    private Hook<ConfirmPlantDelegate>? plantHook;
    private unsafe delegate void SignboardDelegate(AgentHousingSignboard* agent, HousingSignboardPacket* packet);
    private Hook<SignboardDelegate>? signboardHook;
    private readonly ConcurrentQueue<PlacardDetails> placards = new();
    private readonly Dictionary<string, PlacardDetails> estateNames = [];
    private DateTimeOffset nextDiscovery;
    private string discoveryStatus = "Open your FC member list once if its name is not loaded.";
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
    private readonly ConcurrentQueue<(DateTimeOffset At, uint Id, int?[] Parameters, GardenContext? Context, TendIntent? Intent, PlantIntent? Plant)> messages = new();
    private readonly JsonSerializerOptions json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private bool visible = true;
    private volatile bool recording;
    private DateTimeOffset recordingUntil;
    private DateTimeOffset nextSample;
    private ulong character;
    private readonly ConcurrentQueue<GardenMenu> readyMenus = new();
    private readonly ConcurrentQueue<CropChat> cropChats = new();
    private readonly CropChatMatcher cropMatcher = new();
    private readonly Dictionary<string, bool> knownCropItems = new(StringComparer.Ordinal);
    private string cropChatStatus = "Waiting for a mature-bed system message.";
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
                var retained = config.Houses.Select(h => h.EventId).Concat(config.Tending.Select(t => t.EventId)).Concat(config.Planting.Select(t => t.EventId)).Concat(config.Discoveries.Select(e => e.Id)).ToHashSet();
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
            .Concat(config.Planting.Where(t => !sent.Contains(t.EventId)).Select(t => new SyncEvent(t.EventId, "garden.planted", t.ConfirmedAt, WithWorldNames(t.Actor), WithAddressNames(t.Address), t.Patch, t.Bed, t.Plant)))
            .Concat(config.Discoveries.Where(e => !sent.Contains(e.Id) && (e.Kind is "garden.ready" or "garden.observed" ? config.TrackGardens : e.Kind == "character.updated" ? config.SyncCharacterDetails : config.SyncHouseDetails)))
            .OrderBy(e => e.At).Take(50).ToArray();
        while (events.Length > 1 && JsonSerializer.SerializeToUtf8Bytes(new { events }, json).Length > 60000) events = events[..^1];
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
        try { plantHook = Interop.HookFromAddress<ConfirmPlantDelegate>(AgentHousingPlant.MemberFunctionPointers.ConfirmSeedAndSoilSelection, ObservePlantSelection); }
        catch (Exception ex) { Log.Error(ex, "Plant selection observer unavailable"); }
        try { signboardHook = Interop.HookFromAddress<SignboardDelegate>(AgentHousingSignboard.MemberFunctionPointers.ReadPacket, ObserveSignboard); signboardHook.Enable(); }
        catch (Exception ex) { Log.Error(ex, "Estate placard observer unavailable"); }
        if (config.TrackGardens) { callbackHook?.Enable(); plantHook?.Enable(); }
        Commands.AddHandler("/equinox", new CommandInfo(OnCommand) { HelpMessage = "Open Equinox Companion test recorder." });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenMainUi += Open;
        Pi.UiBuilder.OpenConfigUi += Open;
        Framework.Update += Update;
        Chat.LogMessage += OnLog;
        Chat.ChatMessage += OnGardenChat;
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
            snapshot = null; currentAddress = null; Volatile.Write(ref pendingTend, null); Volatile.Write(ref pendingPlant, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null); messages.Clear(); menuMessages.Clear(); readyMenus.Clear(); cropChats.Clear(); cropMatcher.Clear(); status = "Waiting for the area to finish loading."; return;
        }
        if (now >= nextDiscovery)
        {
            nextDiscovery = now.AddSeconds(5);
            try { ObserveDetails(now); } catch (Exception ex) { discoveryStatus = "Details not available yet; try opening your FC member list."; Log.Debug(ex, "Details observation deferred"); }
        }
        if (now < nextSample) return;
        nextSample = now.AddMilliseconds(ObservingGardens ? 50 : 250);
        try
        {
            var manager = HousingManager.Instance();
            if (manager == null || manager->CurrentTerritory == null)
            {
                // Ordinary non-housing zone. A transient load must not create a visit.
                snapshot = null; currentAddress = null; Volatile.Write(ref pendingTend, null); Volatile.Write(ref pendingPlant, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null);
                if (Client.TerritoryType != 0) gate.Observe("outside", now);
                status = "No housing territory loaded.";
                DrainMessages();
                return;
            }
            if (!manager->CurrentTerritory->IsLoaded()) { Volatile.Write(ref pendingTend, null); Volatile.Write(ref pendingPlant, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null); messages.Clear(); menuMessages.Clear(); readyMenus.Clear(); cropChats.Clear(); cropMatcher.Clear(); return; }
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

            if (!ObservingGardens) { snapshot = null; messages.Clear(); menuMessages.Clear(); readyMenus.Clear(); cropChats.Clear(); cropMatcher.Clear(); return; }

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
                        string? first = null, second = null;
                        if (addon->AtkValuesCount > 8)
                        {
                            if (((int)addon->AtkValues[7].Type & 15) is 8 or 10) first = CopyMenuText(addon->AtkValues[7].String.Value);
                            if (((int)addon->AtkValues[8].Type & 15) is 8 or 10) second = CopyMenuText(addon->AtkValues[8].String.Value);
                        }
                        var count = GardenMenu.VisibleOptionCount(addon->AtkValues[5].Int, addon->AtkValuesCount - 7, first, second);
                        var titleType = (int)addon->AtkValues[2].Type & 15;
                        if (count is > 0 and <= 16 && 7 + count <= addon->AtkValuesCount && titleType is 8 or 10)
                        {
                            var title = CopyMenuText(addon->AtkValues[2].String.Value) ?? "";
                            var options = new string[count];
                            var valid = true;
                            for (var j = 0; j < count; j++)
                            {
                                var optionType = (int)addon->AtkValues[7 + j].Type & 15;
                                if (optionType is 0 or 1) { options[j] = ""; continue; }
                                if (optionType is not (8 or 10)) { valid = false; break; }
                                options[j] = CopyMenuText(addon->AtkValues[7 + j].String.Value) ?? "";
                            }
                            if (valid)
                            {
                                var menu = new GardenMenu((nint)addon, now, candidate, title, options);
                                Volatile.Write(ref activeGardenMenu, menu);
                                if (menu.ReadyLocation() is not null && readyMenus.Count < 128) readyMenus.Enqueue(menu);
                            }
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
                addon = args.AddonName, lifecycle = type.ToString(), valueCount = addon->AtkValuesCount, resolvedMenu = Volatile.Read(ref activeGardenMenu)?.Title,
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

    private unsafe void ObservePlantSelection(AgentHousingPlant* agent)
    {
        try
        {
            Volatile.Write(ref pendingPlant, null);
            var now = DateTimeOffset.UtcNow;
            var target = Volatile.Read(ref capturedContext)?.CandidateAt(now);
            if (ObservingGardens && agent != null && Player.IsLoaded && target?.Actor.ContentId == Player.ContentId.ToString(CultureInfo.InvariantCulture) &&
                target.TargetDetails?.DataId == 2003757 && target.Address is not null && !Conditions[ConditionFlag.BetweenAreas] && !Conditions[ConditionFlag.BetweenAreas51])
            {
                var soil = agent->SelectedItems[0].ItemId; var seed = agent->SelectedItems[1].ItemId;
                var sheet = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
                var seedName = sheet.GetRowOrDefault(seed)?.Name.ToString(); var soilName = sheet.GetRowOrDefault(soil)?.Name.ToString();
                if (seed != 0 && soil != 0 && !string.IsNullOrWhiteSpace(seedName) && !string.IsNullOrWhiteSpace(soilName))
                    Volatile.Write(ref pendingPlant, new(Guid.NewGuid().ToString("N"), now, target, new(seed, seedName, soil, soilName)));
            }
        }
        catch (Exception ex) { Log.Error(ex, "Could not copy plant selection"); }
        plantHook!.Original(agent);
    }

    private unsafe void ObserveSignboard(AgentHousingSignboard* agent, HousingSignboardPacket* packet)
    {
        try
        {
            if (Player.IsLoaded && packet != null && placards.Count < 32)
            {
                var address = AddressOf(packet->HouseId);
                if (address is not null && !address.Apartment && !address.Workshop && address.Room == 0 && packet->Size <= 2)
                    placards.Enqueue(new(Player.ContentId.ToString(CultureInfo.InvariantCulture), address, packet->NameString,
                        packet->Size switch { 0 => "Small", 1 => "Medium", _ => "Large" }, packet->EstateType));
            }
        }
        catch (Exception ex) { Log.Error(ex, "Could not copy estate placard"); }
        signboardHook!.Original(agent, packet);
    }

    private void KeepDiscovery(SyncEvent e)
    {
        var last = config.Discoveries.LastOrDefault(x => x.Kind == e.Kind && x.Actor.ContentId == e.Actor.ContentId && x.Address?.HouseId == e.Address?.HouseId && x.Patch == e.Patch && x.Bed == e.Bed);
        if (last is not null && !(e.Kind is "garden.ready" or "garden.observed" && config.Planting.Any(p => p.Actor.ContentId == e.Actor.ContentId && p.Address.HouseId == e.Address?.HouseId && p.Patch == e.Patch && p.Bed == e.Bed && p.ConfirmedAt > last.At)) && JsonSerializer.Serialize(new { last.Actor, last.Address, last.House, last.Character, last.Crop }) == JsonSerializer.Serialize(new { e.Actor, e.Address, e.House, e.Character, e.Crop })) return;
        config.Discoveries.Add(e);
        if (config.Discoveries.Count > 2000) config.Discoveries.RemoveRange(0, config.Discoveries.Count - 2000);
        Pi.SavePluginConfig(config);
    }

    private unsafe void ObserveDetails(DateTimeOffset now)
    {
        var actor = ReadActor();
        while (placards.TryDequeue(out var placard)) if (placard.CharacterId == actor.ContentId) estateNames[placard.Address.HouseId] = placard;
        FreeCompanyDetails? fc = null;
        var proxy = InfoProxyFreeCompany.Instance(); var members = InfoProxyFreeCompanyMember.Instance();
        // The general FC proxy can show another FC. Require our character in the matching member list.
        if (proxy != null && members != null && proxy->Id != 0 && proxy->Id == members->FreeCompanyId && proxy->HomeWorldId == actor.HomeWorldId &&
            members->EntryCount is > 0 and <= 512 && members->CharData != null)
        {
            for (var i = 0; i < (int)members->EntryCount; i++)
                if (members->CharData[i].ContentId == Player.ContentId && members->CharData[i].HomeWorld == actor.HomeWorldId)
                    fc = new(proxy->Id.ToString(CultureInfo.InvariantCulture), proxy->NameString, members->CharData[i].FCTagString, proxy->HomeWorldId, proxy->MasterString);
        }
        if (fc is not null && string.IsNullOrWhiteSpace(fc.Name)) fc = null;
        var jobs = DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>(Dalamud.Game.ClientLanguage.English)
            .Where(j => j.RowId > 0 && j.ExpArrayIndex >= 0).Select(j => new JobDetails(j.RowId, j.Abbreviation.ToString(), Player.GetClassJobLevel(j))).Where(j => j.Level > 0).ToArray();
        var jobName = DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(Player.ClassJob.RowId)?.Abbreviation.ToString() ?? "";
        var info = new CharacterDetails(Player.ClassJob.RowId, jobName, Player.Level, jobs.Select(j => j.Level).DefaultIfEmpty(Player.Level).Max(),
            Player.Race.Value.Masculine.ToString(), Player.Tribe.Value.Masculine.ToString(), (Player.Sex == 0 ? "Male" : "Female"), jobs, fc, jobs.Where(j => j.Id < 8 || j.Id > 18).Select(j => j.Level).DefaultIfEmpty(0).Max());
        KeepDiscovery(new(Guid.NewGuid().ToString("N"), "character.updated", now, actor, null, Character: info));
        var manager = HousingManager.Instance();
        if (manager == null || manager->CurrentTerritory == null || !manager->CurrentTerritory->IsLoaded()) return;
        var type = manager->GetCurrentHousingTerritoryType();
        var current = AddressOf(type == HousingTerritoryType.Indoor ? manager->GetCurrentIndoorHouseId() : manager->GetCurrentHouseId());
        if (current is null || current.Room != 0 || current.Apartment || current.Workshop) return;
        foreach (var estate in new[] { EstateType.FreeCompanyEstate, EstateType.PersonalEstate })
        {
            var owned = AddressOf(HousingManager.GetOwnedHouseId(estate));
            if (owned is null || owned.HouseId != current.HouseId || owned.WorldId != actor.HomeWorldId || estate == EstateType.FreeCompanyEstate && fc is null) continue;
            var size = "";
            if (type == HousingTerritoryType.Outdoor && manager->OutdoorTerritory != null)
            {
                var plot = manager->OutdoorTerritory->Plots[current.Plot - 1];
                if (plot.State == PlotState.OwnedEstate) size = plot.Size switch { PlotSize.Small => "Small", PlotSize.Medium => "Medium", PlotSize.Large => "Large", _ => "" };
            }
            if (size.Length == 0) size = config.Discoveries.LastOrDefault(x => x.Kind == "house.discovered" && x.Actor.ContentId == actor.ContentId && x.Address?.HouseId == current.HouseId)?.House?.Size ?? "";
            estateNames.TryGetValue(current.HouseId, out var sign);
            if (sign is not null && (sign.CharacterId != actor.ContentId || sign.EstateType != (byte)estate)) sign = null;
            if (sign is not null) size = sign.Size;
            var estateName = sign?.Name ?? config.Discoveries.LastOrDefault(x => x.Kind == "house.discovered" && x.Actor.ContentId == actor.ContentId && x.Address?.HouseId == current.HouseId)?.House?.EstateName ?? "";
            var house = new HouseDetails(estate == EstateType.FreeCompanyEstate ? "Free Company house" : "Private house", size, "owned-estate-id", estate == EstateType.FreeCompanyEstate ? fc : null, estateName);
            KeepDiscovery(new(Guid.NewGuid().ToString("N"), "house.discovered", now, actor, current, House: house));
            discoveryStatus = estate == EstateType.FreeCompanyEstate ? $"FC estate detected: {fc!.Name} · W{current.Ward} P{current.Plot}" : $"Private estate detected: W{current.Ward} P{current.Plot}";
        }
    }

    private void OnGardenChat(IHandleableChatMessage message)
    {
        if (!ObservingGardens || cropChats.Count >= 128 || !Player.IsLoaded ||
            Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51] ||
            message.LogKind is not (XivChatType.SystemMessage or XivChatType.GatheringSystemMessage or XivChatType.Notice)) return;
        var now = DateTimeOffset.UtcNow;
        var target = Volatile.Read(ref capturedContext)?.CandidateAt(now);
        if (target?.TargetDetails?.DataId != 2003757 || target.Address is null || target.Actor.ContentId != Player.ContentId.ToString(CultureInfo.InvariantCulture)) return;
        // A newly selected bed must not inherit the previous framework snapshot.
        if (Targets.Target is { } current && current.GameObjectId.ToString("X16") != target.TargetId) return;
        var text = message.OriginalMessage.ExtractText();
        var sender = message.OriginalSender.ExtractText();
        if (text.Length > 512 || sender.Length > 100) return;
        cropChats.Enqueue(new(now, target, text, sender));
        if (recording && text.Contains(CropChatMatcher.ReadyText, StringComparison.Ordinal) && menuMessages.Count < 128)
            menuMessages.Enqueue(new(now, "garden.chatObservation", new { chatType = message.LogKind.ToString(), text, sender, candidateTarget = target }));
    }

    private bool IsKnownCropItem(string name)
    {
        if (knownCropItems.TryGetValue(name, out var known)) return known;
        known = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English).Any(x => x.Name.ToString() == name);
        if (knownCropItems.Count < 512) knownCropItems[name] = known;
        return known;
    }

    private void OnLog(ILogMessage message)
    {
        // No game writes. Copy numeric values for action matching
        // while the native message is valid; inspect housing later on the framework thread.
        if (!ObservingGardens || messages.Count >= 256) return;
        var count = Math.Min((int)message.ParameterCount, 16);
        var parameters = new int?[count];
        for (var i = 0; i < count; i++) if (message.TryGetIntParameter(i, out var n)) parameters[i] = n;
        // Local diagnostic only: garden-system string arguments may identify an existing crop.
        // Never collect player chat, format native messages, or upload diagnostic text.
        var now = DateTimeOffset.UtcNow;
        if (recording && message.LogMessageId is >= 4005 and <= 4025 && menuMessages.Count < 128 &&
            Volatile.Read(ref capturedContext)?.CandidateAt(now)?.TargetDetails?.DataId == 2003757)
        {
            var textParameters = new string?[count];
            for (var i = 0; i < count; i++)
                if (message.TryGetStringParameter(i, out var value))
                {
                    var text = value.ToString();
                    textParameters[i] = text.Length > 512 ? text[..512] : text;
                }
            menuMessages.Enqueue(new(now, "garden.logTextObservation", new { logMessageId = message.LogMessageId, textParameters, parameters }));
        }
        messages.Enqueue((DateTimeOffset.UtcNow, message.LogMessageId, parameters, Volatile.Read(ref capturedContext), Volatile.Read(ref pendingTend), Volatile.Read(ref pendingPlant)));
    }

    private void DrainMessages()
    {
        while (readyMenus.TryDequeue(out var menu))
        {
            cropMatcher.Add(menu);
            if (menu.ReadyLocation() is { } location && menu.Target.Address is { } address)
                KeepDiscovery(new(Guid.NewGuid().ToString("N"), "garden.ready", menu.OpenedAt,
                    WithWorldNames(menu.Target.Actor), WithAddressNames(address), location.Patch, location.Bed));
        }
        while (cropChats.TryDequeue(out var chat)) cropMatcher.Add(chat);
        foreach (var observed in cropMatcher.Drain(DateTimeOffset.UtcNow, IsKnownCropItem))
        {
            KeepDiscovery(new(Guid.NewGuid().ToString("N"), "garden.observed", observed.At,
                WithWorldNames(observed.Target.Actor), WithAddressNames(observed.Target.Address!), observed.Patch, observed.Bed, Crop: observed.Crop));
            cropChatStatus = $"{observed.Crop.CropName} · patch {observed.Patch}, bed {observed.Bed} · ready to harvest";
        }
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
            var planted = message.Plant?.Confirm(message.Id, message.Parameters, message.At, candidate);
            if (planted is not null && !config.Planting.Any(x => x.EventId == planted.EventId))
            {
                config.Planting.Add(planted);
                if (config.Planting.Count > 10000) config.Planting.RemoveRange(0, config.Planting.Count - 10000);
                Pi.SavePluginConfig(config);
            }
            if (message.Id is >= 4005 and <= 4009) Volatile.Write(ref pendingPlant, null);
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
        diagnostics.Clear(); recentSignals.Clear(); menuObservations = 0; messages.Clear(); menuMessages.Clear(); readyMenus.Clear(); cropChats.Clear(); cropMatcher.Clear(); lastSnapshotKey = "";
        Volatile.Write(ref pendingTend, null); Volatile.Write(ref pendingPlant, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null); snapshot = null; nextSample = default;
        lastSubmittedOption = "None recorded";
        callbackHook?.Enable(); plantHook?.Enable();
        recordingUntil = DateTimeOffset.UtcNow.AddMinutes(5); recording = true; exportPath = null;
    }

    private void StopRecording() { recording = false; if (!ObservingGardens) { callbackHook?.Disable(); plantHook?.Disable(); } Volatile.Write(ref pendingTend, null); Volatile.Write(ref pendingPlant, null); Volatile.Write(ref capturedContext, null); Volatile.Write(ref activeGardenMenu, null); messages.Clear(); menuMessages.Clear(); readyMenus.Clear(); cropChats.Clear(); cropMatcher.Clear(); lastSnapshotKey = ""; }

    private void Export()
    {
        try
        {
            var dir = Path.Combine(Pi.GetPluginConfigDirectory(), "exports");
            Directory.CreateDirectory(dir);
            exportPath = Path.Combine(dir, $"equinox-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            File.WriteAllText(exportPath, JsonSerializer.Serialize(new {
                schemaVersion = 3, pluginVersion = "0.4.0.3", exportedAt = DateTimeOffset.UtcNow,
                mode = "local-diagnostics", gardeningConfirmed = false,
                houseObservations = config.Houses, confirmedTending = config.Tending, confirmedPlanting = config.Planting, observedDetails = config.Discoveries, diagnostics
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
            ImGui.TextWrapped("Local tracking version 0.4.0.3 — optional website connection available.");
            if (ImGui.CollapsingHeader("Website connection"))
            {
                ImGui.TextWrapped("First deploy Journal V7.9.21, then open Game connection on the website and create a pairing key.");
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
                var characters = config.SyncCharacterDetails; var houses = config.SyncHouseDetails;
                if (ImGui.Checkbox("Sync character and job details", ref characters)) { config.SyncCharacterDetails = characters; Pi.SavePluginConfig(config); }
                if (ImGui.Checkbox("Discover/update owned private and FC houses", ref houses)) { config.SyncHouseDetails = houses; Pi.SavePluginConfig(config); }
                ImGui.TextWrapped(syncStatus);
                ImGui.TextWrapped("Sends character and job details, confirmed owned-estate and FC details, property addresses, planting and tending records, and entry times. Pairing key is saved on this PC and is never included in test exports. Characters match automatically by name and home server. Unmatched houses and patches need linking once.");
            }
            ImGui.Separator(); ImGui.TextWrapped(status);
            ImGui.TextWrapped(discoveryStatus);
            ImGui.TextWrapped(cropChatStatus);
            ImGui.TextWrapped($"Saved planting records: {config.Planting.Count}. Plant observer: {(plantHook is null ? "unavailable" : "ready")}");
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
            if (ImGui.CollapsingHeader("Detected character and estate details"))
            {
                var own = config.Discoveries.Where(x => x.Actor.ContentId == Player.ContentId.ToString(CultureInfo.InvariantCulture));
                var details = own.LastOrDefault(x => x.Kind == "character.updated");
                if (details?.Character is { } ch) ImGui.TextWrapped($"{details.Actor.Name} · {details.Actor.HomeWorldName} · {ch.JobName} Lv. {ch.Level} · highest combat level {ch.HighestBattleLevel}");
                foreach (var h in own.Where(x => x.Kind == "house.discovered").GroupBy(x => x.Address!.HouseId).Select(g => g.Last()))
                    ImGui.TextWrapped($"{h.House!.Type} · {h.House.EstateName} · {h.House.FreeCompany?.Name} · {h.House.Size} · W{h.Address!.Ward} P{h.Address.Plot}");
                ImGui.TextWrapped("Open your estate placard to read its name and size. Open the FC member list with your character visible if FC details are missing. Only confirmed owned estate addresses are discovered.");
            }
            ImGui.Separator();
            var track = config.TrackGardens;
            if (ImGui.Checkbox("Automatically save confirmed planting and tending locally", ref track))
            {
                config.TrackGardens = track;
                if (ObservingGardens) { callbackHook?.Enable(); plantHook?.Enable(); } else StopRecording();
                Pi.SavePluginConfig(config);
            }
            ImGui.TextWrapped("English garden menus supported. Confirmed tending is saved per house, patch and bed. Website matching preserves existing batches. Planting records include the selected seed and soil plus a successful game response. Harvest syncing is not enabled.");
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
        plantHook?.Disable(); plantHook?.Dispose();
        signboardHook?.Disable(); signboardHook?.Dispose();
        Chat.LogMessage -= OnLog;
        Chat.ChatMessage -= OnGardenChat;
        foreach (var menuEvent in MenuEvents) Addons.UnregisterListener(menuEvent, GardenMenus, OnGardenMenu);
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenMainUi -= Open;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Commands.RemoveHandler("/equinox");
    }
}
