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
