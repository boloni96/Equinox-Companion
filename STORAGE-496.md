# 0.5.1.496 — Individual dresser selection and confirmation

The 0.5.1.495 report showed two individual-storage attempts closing both dresser windows immediately after row selection. No confirmation was sent; later movement cancelled an already stalled operation.

Replace the incorrect list callback 0 with the normal-item agent ReceiveEvent: three unsigned values (14, matched candidate index, 0), event kind 0. Resolve the visible, ready confirmation through the dresser agent ContextAddonId instead of assuming SelectYesno. Keep exact container/slot/item ownership, live-copy eligibility and item-name checks before confirming once with FireCallbackInt(0). Successful storage still requires inventory removal and destination presence. No item action is retried. Missing confirmation now reports a specific failure after eight seconds instead of waiting through generic deadline extensions.

Reference inspected: [SpringCleaning NativeUi.OpenDresserStorage / GetDresserItemConfirmationAddon / ConfirmDresserItemStorage](https://github.com/OhGodAGirl/FFXIV/blob/95f5b9bd9ed5812f58c3c4f6e31bd68e7f504eaa/SpringCleaning/src/Features/Migration/Interop/NativeUi.cs). FFXIVClientStructs documents ContextAddonId as the active crystallize/YesNo/prism-set context dialog ID.

Diagnostics record the selected candidate, inventory location, owned confirmation addon and receipt. Dye, gear-set, materia, damage, duplicate and keep protections remain enforced. No changes to the separate armoire-only runner or outfit storage.

Validation: Release build and gate suite. In-game individual dresser transfer remains to be tested; these checks cannot simulate the client UI event handler.
