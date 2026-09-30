# 0.4.0.3 — mature menu/chat association fix

Keep Journal V7.9.21 and the same pairing key. This is a plugin-only update.

Mature menus expose Harvest Crop and Quit while retaining count metadata used by growing menus. Read that exact bounded pair without assuming all declared entries are visible. Growing-menu callback indices remain unchanged. Additional diagnostic metadata reports the actual value count and resolved menu title.

The original game system chat reader remains enabled: identify any known English game-item crop name from the ready-to-harvest message and associate it with the same character, house and bed menu. It updates crop name/readiness, never inventing planting, watering or harvesting. No player chat is uploaded.

Validation: Release build and gate suite pass. Local replay of the user's slow/quick diagnostic matches all 17 messages to eight unique beds, including a repeated bed, with zero mismatches. Private diagnostics are not committed. Repeat the live opening/cancelling test after updating to verify native menu reading and upload.

