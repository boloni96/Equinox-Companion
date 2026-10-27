using System.Runtime.CompilerServices;
using System.Text;
using EquinoxCompanion;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AtkType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

internal static unsafe class TradeItemShortcut260Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var checks = 0;
        void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Trade item shortcut: " + why); }
        var label = Encoding.UTF8.GetBytes("Trade\0");
        var plugin = Encoding.UTF8.GetBytes("E Trade\0");
        var other = Encoding.UTF8.GetBytes("Discard\0");
        var localized = Encoding.UTF8.GetBytes("交換\0");
        fixed (byte* trade = label, custom = plugin, discard = other, local = localized)
        {
            var values = stackalloc AtkValue[16];
            for (var i = 0; i < 16; i++) values[i] = default;
            values[0].Type = AtkType.UInt; values[0].UInt = 3;
            values[2].Type = values[3].Type = AtkType.UInt;
            values[8].Type = values[9].Type = values[10].Type = AtkType.String;
            values[8].String = custom; values[9].String = discard; values[10].String = trade;
            for (var i = 11; i < 14; i++) values[i].Type = AtkType.Int;
            Check(TradeItemShortcutNative.DisplayedEntry(values, 11, "Trade") == 2, "uses shifted display index, not native index");
            Check(TradeItemShortcutNative.DisplayedEntry(values, 14, "Trade") == 2, "enabled menu with disabled-state array");
            values[13].Int = 1;
            Check(TradeItemShortcutNative.DisplayedEntry(values, 14, "Trade") == -1, "disabled Trade must not fire");
            values[13].Int = 0; values[3].UInt = 4;
            Check(TradeItemShortcutNative.DisplayedEntry(values, 14, "Trade") == -1, "submenu must not fire");
            values[3].UInt = 0; values[2].UInt = 4;
            Check(TradeItemShortcutNative.DisplayedEntry(values, 14, "Trade") == -1, "return entry must not fire");
            values[2].UInt = 0; values[8].String = trade;
            Check(TradeItemShortcutNative.DisplayedEntry(values, 14, "Trade") == -1, "ambiguous plugin label must not select anything");
            values[8].String = custom; values[10].String = local;
            Check(TradeItemShortcutNative.DisplayedEntry(values, 14, "交換") == 2, "exact localized native label");
            Check(TradeItemShortcutNative.DisplayedEntry(values, 14, "Trade") == -1, "never fall back to Discard or plugin action");
            Check(TradeItemShortcutNative.DisplayedEntry(values, 12, "交換") == -1, "malformed menu length rejected");
            values[0].UInt = 33;
            Check(TradeItemShortcutNative.DisplayedEntry(values, 14, "交換") == -1, "out of bounds count rejected");
            Check(TradeItemShortcutNative.DisplayedEntry(null, 14, "Trade") == -1, "null menu rejected");

            AgentInventoryContext agent = default;
            agent.ContexItemStartIndex = 8; agent.ContextItemCount = 2;
            agent.EventParams[8].Type = agent.EventParams[9].Type = AtkType.String;
            agent.EventParams[8].String = discard; agent.EventParams[9].String = trade;
            var labels = new HashSet<string>(StringComparer.Ordinal) { "Trade" };
            Check(TradeItemShortcutNative.NativeEntry(&agent, labels) == 1, "native enabled Trade found");
            agent.ContextItemDisabledMask = 2;
            Check(TradeItemShortcutNative.NativeEntry(&agent, labels) == -1, "disabled native Trade rejected even if rendered enabled");
            agent.ContextItemDisabledMask = 0; agent.ContextItemSubmenuMask = 2;
            Check(TradeItemShortcutNative.NativeEntry(&agent, labels) == -1, "native submenu rejected");
            agent.ContextItemSubmenuMask = 0; agent.EventParams[8].String = trade;
            Check(TradeItemShortcutNative.NativeEntry(&agent, labels) == -1, "ambiguous native labels rejected");
            agent.ContexItemStartIndex = 97;
            Check(TradeItemShortcutNative.NativeEntry(&agent, labels) == -1, "native parameter overrun rejected");
        }

        var target = new TradeItemShortcutTarget(10, 20, 30, 40, 0, 2, 100, 0, 99);
        var request = new TradeItemShortcutRequest(target, "Trade", 1000);
        Check(request.Matches(target, 1050), "unchanged stack accepted");
        Check(!request.Matches(target, 999), "invalid monotonic time rejected");
        Check(!request.Matches(target, 1751), "stale click expires");
        Check(!request.Matches(target with { Character = 11 }, 1050), "different character rejected");
        Check(!request.Matches(target with { Partner = 21 }, 1050), "different trade partner rejected");
        Check(!request.Matches(target with { TradeAddon = 31 }, 1050), "replacement trade window rejected");
        Check(!request.Matches(target with { ItemPointer = 41 }, 1050), "replacement item pointer rejected");
        Check(!request.Matches(target with { Inventory = 1 }, 1050), "same item in another bag rejected");
        Check(!request.Matches(target with { Slot = 3 }, 1050), "same item in another slot rejected");
        Check(!request.Matches(target with { ItemId = 101 }, 1050), "different item rejected");
        Check(!request.Matches(target with { Flags = 1 }, 1050), "NQ to HQ slot change rejected");
        Check(!request.Matches(target with { Quantity = 98 }, 1050), "changed stack rejected");
        Console.WriteLine($"Trade item shortcut regression checks passed: {checks}.");
    }
}
