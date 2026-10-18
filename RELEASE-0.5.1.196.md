# Companion 0.5.1.196

Fix the seasonal quest catalogue dropping valid quests whose Festival row has an empty Name. All Saints’ Wake 2026 uses festival 176, whose game-data name is blank. It now receives a fallback label and quest 71004 reaches the existing completion scanner and upload validation. Other unnamed festivals receive a neutral numeric label. Nonempty metadata validation remains unchanged.

Regression tests cover the real blank festival row, future unnamed festivals, existing names, and upload validation of final quest 71004. Requires the existing Journal V7.11.199 deployment; no new Journal ZIP. Both players should update Companion and log into affected characters to observe their actual quest completion. No completion is inferred from reward ownership.
