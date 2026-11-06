# Companion 0.5.1.461 — Framer's Kit confirmation

The supplied 0.5.1.451 report shows all 19 purchases completing. The Keybound Brawler Framer's Kit was then consumed (bags 1 to 0), but generic unlock state stayed at 2 until the use flow timed out. By export time the generic check reported 1.

Two current-code defects prevented reliable specific-collection confirmation:
1. The collection catalogue uses Journal IDs. This kit is catalogue ID 1101, while Item 51997 AdditionalData is native kit ID 345. Passing 1101 to PlayerState.IsFramersKitUnlocked queried the wrong ID.
2. The fallback looked only at catalogueBase, which is cleared after collectionGroups finishes loading.

All Framer's Kit observations now resolve the native ID from the game's Item.AdditionalData. The item-to-kit mapping is cached; ownership is read live for the current character. Journal catalogue IDs remain unchanged. Keybound's framer reward check does not require collection sync to be enabled. Other reward fallbacks use the completed collection catalogue when available.

Exports include the native kit ID and catalogue loading state. No changes to purchase confirmation, no repeated use while waiting, and no assumption that disappearance from inventory proves unlocking.

Validation: plugin build, Keybound and transition suites pass. Regression checks cover the 51997/1101/345 distinction, consumption plus positive unlock, character changes, unavailable metadata and refusal to confirm from consumption alone. Live confirmation on a character with an unused kit remains necessary.

Reference data: https://v2.xivapi.com/api/sheet/Item/51997?fields=Name,AdditionalData
Native API contract: https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/PlayerState.cs
