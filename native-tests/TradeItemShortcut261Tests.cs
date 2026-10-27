using System.Runtime.CompilerServices;
using EquinoxCompanion;
using FFXIVClientStructs.FFXIV.Client.Game;

internal static unsafe class TradeItemShortcut261Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var checks = 0;
        void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("All-inventory trade: " + why); }

        InventoryItem source = default;
        var container = new InventoryContainer { Items = &source, Size = 80, IsLoaded = true };
        InventoryType[] types =
        [
            InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
            InventoryType.ArmoryOffHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody, InventoryType.ArmoryHands,
            InventoryType.ArmoryLegs, InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist, InventoryType.ArmoryRings, InventoryType.ArmoryMainHand, InventoryType.Crystals,
            InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2
        ];
        // Container support alone does not authorize an action: the native
        // enabled Trade-entry checks from .260 still gate every selection.
        foreach (var type in types)
        {
            container.Type = source.Container = type;
            source.Slot = 49; source.ItemId = 123; source.Quantity = 99;
            Check(TradeItemShortcutNative.ContainerSlotAvailable(&container, type, 49), "actual container capacity supports " + type);
            Check(TradeItemShortcutNative.CaptureItem(&source, &source, type, 49, out var item), "stack identity captured from " + type);
            Check(item.Inventory == (uint)type && item.Slot == 49 && item.Quantity == 99, "container and full stack retained for " + type);
        }
        container.Type = source.Container = InventoryType.ArmoryMainHand;
        source.Slot = 49;
        Check(!TradeItemShortcutNative.ContainerSlotAvailable(&container, container.Type, -1), "negative slot");
        Check(!TradeItemShortcutNative.ContainerSlotAvailable(&container, container.Type, 80), "slot at capacity");
        Check(TradeItemShortcutNative.ContainerSlotAvailable(&container, container.Type, 79), "last available slot");
        Check(!TradeItemShortcutNative.ContainerSlotAvailable(&container, InventoryType.Inventory1, 49), "wrong container identity");
        Check(!TradeItemShortcutNative.ContainerSlotAvailable(null, container.Type, 49), "missing container");
        Check(!TradeItemShortcutNative.ContainerSlotAvailable(&container, InventoryType.Invalid, 49), "invalid container");
        container.IsLoaded = false;
        Check(!TradeItemShortcutNative.ContainerSlotAvailable(&container, container.Type, 49), "unloaded container");
        container.IsLoaded = true; container.Items = null;
        Check(!TradeItemShortcutNative.ContainerSlotAvailable(&container, container.Type, 49), "missing item array");
        container.Items = &source;
        InventoryItem dummy = source;
        Check(!TradeItemShortcutNative.CaptureItem(&source, &dummy, source.Container, 49, out _), "matching name/id in a synthetic menu is not the actual clicked slot");
        Check(!TradeItemShortcutNative.CaptureItem(&source, &source, InventoryType.Inventory1, 49, out _), "wrong bag");
        Check(!TradeItemShortcutNative.CaptureItem(&source, &source, source.Container, 48, out _), "wrong slot");
        source.ItemId = 0;
        Check(!TradeItemShortcutNative.CaptureItem(&source, &source, source.Container, 49, out _), "empty item");
        source.ItemId = 123; source.Quantity = 0;
        Check(!TradeItemShortcutNative.CaptureItem(&source, &source, source.Container, 49, out _), "empty stack");
        source.Quantity = 99;
        TradeItemShortcutNative.CaptureItem(&source, &source, source.Container, 49, out var original);
        var request = new TradeItemShortcutRequest(original, "Trade", 1000);
        source.Quantity = 50;
        TradeItemShortcutNative.CaptureItem(&source, &source, source.Container, 49, out var changed);
        Check(!request.Matches(changed, 1050), "stack quantity changed before selection");

        InventoryItem real = new() { Container = InventoryType.Inventory2, Slot = 4, ItemId = 123, Quantity = 99, Flags = InventoryItem.ItemFlags.HighQuality };
        InventoryItem other = real;
        source.IsSymbolic = true;
        var realAddress = (nint)(&real);
        SellerNativeInterop.LinkedItemReader resolve = _ => (InventoryItem*)realAddress;
        Check(TradeItemShortcutNative.CaptureItem(&source, &source, source.Container, 49, out var linked, resolve), "symbolic view slot resolves");
        Check(linked.Inventory == (uint)source.Container && linked.Slot == 49 &&
            linked.ResolvedInventory == (uint)real.Container && linked.ResolvedSlot == 4 &&
            linked.ItemId == 123 && linked.Quantity == 99 && linked.Flags == 1, "both source and resolved identities retained");
        Check(TradeItemShortcutNative.CaptureItem(&source, &real, source.Container, 49, out _, resolve), "native context may target the resolved item");
        Check(!TradeItemShortcutNative.CaptureItem(&source, &dummy, source.Container, 49, out _, resolve), "unrelated context rejected with symbolic source");
        var linkedRequest = new TradeItemShortcutRequest(linked, "Trade", 1000);
        realAddress = (nint)(&other);
        TradeItemShortcutNative.CaptureItem(&source, &source, source.Container, 49, out changed, resolve);
        Check(!linkedRequest.Matches(changed, 1050), "link repointed to identical item rejects stale click");
        Check(!TradeItemShortcutNative.CaptureItem(&source, &source, source.Container, 49, out _, _ => null), "unresolved link");
        Check(!TradeItemShortcutNative.CaptureItem(&source, &source, source.Container, 49, out _, item => item), "cyclic link");

        InventoryManager inventory = default;
        inventory.TradeItemsLocal[0] = real; inventory.TradeItemsLocal[0].Quantity = 10;
        inventory.TradeItemsLocal[1] = real; inventory.TradeItemsLocal[1].Quantity = 2;
        inventory.TradeItemsLocal[2] = real; inventory.TradeItemsLocal[2].Flags = 0;
        inventory.TradeItemsLocal[3] = real; inventory.TradeItemsLocal[3].ItemId = 321;
        inventory.TradeItemsLocal[5] = real; inventory.TradeItemsLocal[5].Quantity = 500; // currency excluded
        inventory.TradeItemsRemote[0] = real; // incoming offers excluded
        Check(TradeItemShortcutNative.OfferedQuantity(&inventory, linked) == 12, "offer evidence counts only matching local item/quality, excludes gil and partner");
        inventory.TradeItemsLocal[1].Quantity = 26;
        Check(TradeItemShortcutNative.OfferedQuantity(&inventory, linked) == 36, "partial stack appears as an increased offer");

        var cleanup = new TradeItemMenuCleanup(1, linked, 12, 1000);
        Check(cleanup.Step(1050, true, false, false) == TradeItemMenuDecision.Wait, "do not close before a delayed quantity prompt");
        Check(cleanup.Step(1100, true, true, false) == TradeItemMenuDecision.Wait, "keep quantity choice open");
        Check(cleanup.Step(1200, true, true, true) == TradeItemMenuDecision.Wait, "never close while quantity dialog is still open");
        Check(cleanup.Step(1500, true, false, true) == TradeItemMenuDecision.Close, "close menu after confirmed quantity/offer");
        cleanup = new(1, linked, 12, 1000);
        cleanup.Step(1100, true, true, false);
        Check(cleanup.Step(1200, true, false, false) == TradeItemMenuDecision.Close, "cancelled quantity dismisses only leftover item menu");
        cleanup = new(1, linked, 12, 1000);
        Check(cleanup.Step(1100, true, false, true) == TradeItemMenuDecision.Close, "single gear/material item added");
        Check(cleanup.Step(1100, false, false, true) == TradeItemMenuDecision.Forget, "replacement menu/partner is never closed");
        Check(cleanup.Step(4101, true, false, false) == TradeItemMenuDecision.Forget, "failed action keeps menu usable");
        Check(cleanup.Step(999, true, false, true) == TradeItemMenuDecision.Forget, "invalid time");
        Check(cleanup.Step(301001, true, true, false) == TradeItemMenuDecision.Forget, "expired watcher never closes a quantity prompt");
        Console.WriteLine($"All-inventory and stack trade regression checks passed: {checks}.");
    }
}
