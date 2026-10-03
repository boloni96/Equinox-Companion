Equinox Companion 0.5.1.38

Harvest confirmation now also reads the original “You obtain…” system/loot chat item link. It matches that item to the crop inspected at the numbered bed where Harvest Crop was submitted. This path does not depend on native receipt log IDs or template parameter mapping.

The selected bed remains available through the harvest animation even when the game drops its target. A fresh snapshot must still confirm the same character and estate; a different selected bed, stale reward, unrelated item or crossbred-seed reward does not clear a crop. Confirmation consumes the intent once and uses the same event ID as native confirmation, preventing double counting. Actual game messages are observed only; no game actions are sent.

The exact bed is marked empty locally and synced using the existing garden.empty path. Local bounded diagnostics record receipt IDs and correlation results. All previous Planting person/account groups and character-bar timers remain included.

Validation: release build has zero warnings/errors. 591 automated checks pass, including original item-link parsing, plural/HQ rewards, target loss, actor/estate/bed mismatch, stale events and native/chat event deduplication identity. Real in-game callback timing still requires a test harvest after updating. Historical harvests without captured evidence are not guessed.

Website V7.11.49 adds Reset plan independently; it preserves actual crops and care records.
