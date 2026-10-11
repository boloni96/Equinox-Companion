# Companion 0.5.1.120 — Find items by name and exact location

`/doihaveit` now opens an independent Dalamud window with normal window settings, Person tabs, account/character grouping and logged-in character priority. The old main-window tab is removed. Inventory right-click lookup shows the full English item name while retaining its exact ID internally. Location, quality and observation-age filters complement account/character selection.

A matching crate-and-magnifier shortcut occupies the second header row beside Gardening. Its floating minimized icon restores the same search. Window opacity/pinning/clickthrough/blur use native settings; floater position/opacity/blur use existing launcher settings. Escape handling for all five existing supported windows and item search now reads Dalamud's game key state with edge detection, respects open popups and minimizes only the focused window once. X still closes without creating a floater.

Storage observations retain one-based container slots and per-slot quantities, with item totals still grouped by item and HQ. Inventory/retainer page numbers remain in location labels. Legacy observations retain unknown slots. Displayed slots are last recorded container order; sorting or moving items requires a fresh observation. Armoire/dresser records have no inventory slot and remain presence-only.

Requires Journal V7.11.128 (protocol17) before new storage observations upload. Other supported event sync continues. Journal reuses its existing Collection/equipment catalogue and Companion-provided names; no separate item catalogue is introduced.

Validation: local Release build against official API15 references; GateTests for quantity/HQ/slot serialization and protocol gating. Journal tests cover authenticated slot ingestion, malformed positions/quantities, browser-closed propagation, legacy name matching, location grouping and filters. Real game Esc, context-menu and slot display still require user validation.

Search/filter selections survive Person and account switching; Main/Alt is matched by account name across Persons. An explicitly selected character remains selected until changed or cleared, even if it has no results in the new scope.
