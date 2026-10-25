# Companion 0.5.1.250 · Gil Trader

Optional `/eqtrade` window for repeated gil-only trades with one nearby party member. No Journal or Worker update is needed.

1. Join the same party and stand beside each other.
2. Open `/eqtrade` on both clients and enable the tool once.
3. The receiver selects the sender, chooses **Receive**, then **Start receiving**.
4. The sender selects the receiver, enters the **total gil**, then presses **Send gil**.

Each trade sends at most 1,000,000 gil, with an exact remainder in the last trade. The recipient may also accept manually without this tool. Stop and `/eqtrade stop` revoke further actions; transfers already completed cannot be undone. X stops; minimizing keeps the session active. Receiving expires after 60 seconds without trade progress. Sessions never resume after restarting or changing character/area.

The tool verifies party identity, the native trade partner, gil amounts, item-free offers, wallet capacity and the game's localized confirmation. It stops on a changed offer, unrelated dialog or uncertain completion. Progress requires a closed trade and the exact corresponding wallet change. A final confirmation already sent to the game may still settle after Stop; the status explicitly says so.

Both Goddess and Classic designs use the same compact window, footer launcher, minimize icon and functionality. Settings → Features enables/disables it. Details and Settings → Diagnostics include a bounded trade trace, with an export and folder shortcut.

## Validation

Offline checks cover split amounts, final remainders, two-sided acceptance, delayed confirmation, cancellation, identity changes, incorrect offers and wallet limits. Native tests use allocated structs to check all five item slots and the separate gil slot on each side, exact localized prompts and registered button events. These checks do not claim a live in-game trade was tested.

First live checks: send **1 gil**, reverse the direction, cancel while waiting, then—only if the small trades work—try **1,000,001 gil** to verify the second trade contains exactly 1 gil. The trade trace records the native stage if a game button or confirmation differs from the build references.

## Research

Reviewed [Henchman's Testy Trader](https://github.com/Knightmore/Henchman/tree/main/Henchman/Features/TestyTrader) and its localized Addon 102223 confirmation lookup, plus [FFXIVClientStructs InventoryManager](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/InventoryManager.cs). The implementation is independent; it does not bundle Henchman, require its RPC service or copy its transfer queue. It uses the supported native structures already referenced by Companion.
