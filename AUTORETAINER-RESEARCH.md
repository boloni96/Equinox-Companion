# AutoRetainer submarine integration investigation — 2026-10-02

Inspected upstream PunishXIV/AutoRetainerAPI at commit 7ccf0f6b4c7923821a43ed1e92456c9d5d7132f2.

Primary sources:
- https://github.com/PunishXIV/AutoRetainerAPI/blob/main/AutoRetainerAPI/AutoRetainerApi.cs
- https://github.com/PunishXIV/AutoRetainerAPI/blob/main/AutoRetainerAPI/Configuration/OfflineCharacterData.cs
- https://github.com/PunishXIV/AutoRetainerAPI/blob/main/AutoRetainerAPI/Configuration/OfflineVesselData.cs
- https://github.com/PunishXIV/AutoRetainerAPI/blob/main/AutoRetainerAPI/Configuration/AdditionalVesselData.cs

The supported API enumerates registered character CIDs via AutoRetainer.GetRegisteredCIDs and reads cached OfflineCharacterData via AutoRetainer.GetOfflineCharacterData. The returned live object must not be retained; copy only the required fields immediately.

OfflineCharacterData includes CID, Name, World, FCID, OfflineSubmarineData and AdditionalSubmarineData. Offline vessel records expose names and ReturnTime; additional records include level, four parts, Points and IndexOverride. The character enumeration excludes uninitialized and blacklisted characters.

This offers a route to reading already-cached submarine records across characters without logging into each character just to read the cache. It does not refresh game state for offline characters. These inspected records have no reliable last-observed timestamp, so reading the cache now must not label old data as a new game observation or overwrite newer direct observations. FC duplication across characters and stale membership require explicit reconciliation.

Future implementation should be optional, read-only IPC with bounded scans, copy allowlisted fields only, label cached source and read time separately from observation time, deduplicate by verified FC identity and vessel identity, and preserve newer game observations. Do not import unrelated retainer/inventory/account fields or call relog/deploy/write APIs. Test against a live AutoRetainer installation before publishing an integration.

No AutoRetainer runtime integration is included in Companion 0.5.1.14; this release contains the settings/shortcuts/tab/diagnostics changes.

## Implementation in 0.5.1.15

The optional integration is now implemented. Live game validation remains pending. The 0.5.1.14 statements above describe the earlier investigation.

Additional inspected primary sources (AutoRetainer master on 2026-10-02):
- AutoRetainer/Modules/OfflineDataManager.cs: WriteOfflineInventoryData reads item 10155 (ceruleum tanks) and 10373 (repair kits) from the character inventory.
- AutoRetainer/Modules/Voyage/VoyageUtils.cs: caches component ItemId (not submarine component row IDs), rank, EXP and CurrentExplorationPoints.
- AutoRetainer/Modules/IPC.cs: registered CID enumeration and cached character getter.
- Installed Dalamud API15 CallGateChannel InvokeFunc/ConvertObject: different return DTO types are serialized/converted by Dalamud. The integration requests a deliberately partial DTO, then copies allowlisted fields; it never retains the provider object or requires AutoRetainerAPI.dll.

No game observation timestamp is available; each reporter retains its own cached view with import time. FC membership must already be confirmed independently. The website never overwrites direct workshop observations from cached data. Missing AutoRetainer or failed IPC defers the optional scan one minute without interrupting tracking.

## Rechecked for Companion 0.5.1.41 / Journal V7.11.52 — 2026-10-03

The existing GitHub research was recovered and extended. Rechecked AutoRetainer source at cc020df0efee3ea65a45117e2b89999108c702e8 and its pinned AutoRetainerAPI at 7ccf0f6b4c7923821a43ed1e92456c9d5d7132f2; the API commit matches the earlier investigation.

- `OfflineCharacterData.InventorySpace` is the number of free slots in the four carried inventory bags. It is distinct from `NumSubSlots` (unlocked submarine capacity). Both values now have separate fields and labels.
- `OfflineDataManager.Tick` refreshes the current character while relevant operations/UI are active, with throttled writes. `WriteOfflineInventoryData` reads free inventory slots and carried item counts for 10155 and 10373. The workshop's `VoyageUtils.WriteOfflineData` also refreshes these counts while capturing vessel timers, rank, experience, component item IDs and route points.
- `GetRegisteredCIDs` excludes blacklisted and uninitialized characters. `GetOfflineCharacterData` returns the stored character object; invoking the getter does not refresh offline game state. No read-only API provides a reliable observation time for these cached values.
- Companion keeps the supported read-only IPC import. It now copies the missing inventory-space field, scans the full paired roster across persons/accounts, continues past an individual failed record, and offers explicit refresh. The source getter and data conversion remain native integration points that require in-game testing.
- A separate direct observation reads only the logged-in character's four loaded bags, recording tanks, repair materials, free slots and total capacity. Counts are synchronized on change and reconfirmed after five minutes of real sampling. Missing/unloaded inventory is never recorded as zero. Cached data cannot overwrite this confirmed observation, even if imported later.
- Supplies remain per character and verified FC; FC fleets are shared. Leaving/changing FC removes old supplies/cache from active projections. Cached vessel data is labelled separately from direct workshop observations because unknown-age cache cannot safely replace a dated direct reading.
- `submarines.supplies` requires protocol 13. Cache inventory is backward-compatible and an explicit rescan is requested when the shared roster upgrades to protocol 13. Existing sources remain optional.

Pinned primary sources:
- https://github.com/PunishXIV/AutoRetainer/blob/cc020df0efee3ea65a45117e2b89999108c702e8/AutoRetainer/Modules/OfflineDataManager.cs
- https://github.com/PunishXIV/AutoRetainer/blob/cc020df0efee3ea65a45117e2b89999108c702e8/AutoRetainer/Modules/Voyage/VoyageUtils.cs
- https://github.com/PunishXIV/AutoRetainer/blob/cc020df0efee3ea65a45117e2b89999108c702e8/AutoRetainer/Helpers/Utils.cs
- https://github.com/PunishXIV/AutoRetainer/blob/cc020df0efee3ea65a45117e2b89999108c702e8/AutoRetainer/Modules/IPC.cs
- https://github.com/PunishXIV/AutoRetainerAPI/blob/7ccf0f6b4c7923821a43ed1e92456c9d5d7132f2/AutoRetainerAPI/Configuration/OfflineCharacterData.cs

No upstream source is copied into the distributed plugin. The importer uses a small independent DTO and the existing allowed APIs; it does not deploy vessels, relog characters, or modify AutoRetainer data.
