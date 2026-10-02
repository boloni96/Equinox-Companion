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
