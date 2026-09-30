# 0.4.0.4

Fix incomplete character snapshots blocking website uploads; add Tests and Characters & housing tabs with recorded-entry colour estimates and estate hover details. Re-enable character/job sync after updating. Same website and pairing key. Purple DEMOLISHED? is an estimate, not confirmed destruction.

# 0.4.0.3 — mature menu/chat association fix

Keep Journal V7.9.21 and the same pairing key. This is a plugin-only update.

Mature menus expose Harvest Crop and Quit while retaining count metadata used by growing menus. Read that exact bounded pair without assuming all declared entries are visible. Growing-menu callback indices remain unchanged. Additional diagnostic metadata reports the actual value count and resolved menu title.

The original game system chat reader remains enabled: identify any known English game-item crop name from the ready-to-harvest message and associate it with the same character, house and bed menu. It updates crop name/readiness, never inventing planting, watering or harvesting. No player chat is uploaded.

Validation: Release build and gate suite pass. Local replay of the user's slow/quick diagnostic matches all 17 messages to eight unique beds, including a repeated bed, with zero mismatches. Private diagnostics are not committed. Repeat the live opening/cancelling test after updating to verify native menu reading and upload.

# 0.4.0.2 — mature crop names from game chat

Deploy Journal V7.9.21 first, then update the plugin in Dalamud. The existing pairing key stays valid. V7.9.21 includes the V7.9.20 estate-name, FC-master and existing-character-details fixes.

- Observe original game system messages containing the English text “This crop is ready to be harvested.” Pair the crop name with a nearby mature garden menu and the same character, estate and target.
- Support combined name/status text, the name in the system sender field, or a separate known-item name within 750 ms before the status. Accept one unambiguous bed menu within two seconds, allowing text just before menu setup.
- Names must match an English game item. Player chat and echo messages are excluded. Unmatched/ambiguous text never updates a bed. Recording captures only relevant ready-message diagnostics; raw chat is not sent to the website.
- Sync garden.observed with crop name and ready state. Existing bed/batch and planting/watering times are kept. A differing recorded crop is saved in history and flagged on the garden map. Unknown planting dates remain unknown.
- Repeat observations of the same crop are deduplicated locally; no new HTTP requests when the queue is empty. Existing upload batching, retries and website polling are unchanged.
- This does not confirm planting, tending or harvesting. Growing-crop message formats are not added in this release. In-game recipe planning remains future work.

## Test

1. Deploy the V7.9.21 Cloudflare ZIP, then update Equinox Companion to 0.4.0.2. Leave the existing key and enable gardening tracking/sync.
2. Start a diagnostic recording. Open a mature Curiel Root bed, let its system message appear, and cancel. Repeat for a Royal Kukuru Bean bed. No harvest needed.
3. Wait one second, stop and export. Keep the website visible with automatic application enabled and close editing/connection dialogs. Allow about 45 seconds for upload and polling.
4. Check exact crop, patch/bed and ready state; the old batch and planting/watering times should be unchanged. The plugin shows its latest matched crop/bed in its window.
5. Send the diagnostic export if names do not arrive or any bed is wrong. Live channel/format verification is still required; automated tests use the supplied English message text and synthetic estate data.

# 0.4.0.1 — existing journal details

Deploy Journal V7.9.20 first. Existing pairing keys still work.

- Send the FC master name from the verified loaded FC proxy. The journal matches an existing character by name and home world, or displays the master name without creating a character.
- Website confirmed estate names now replace placeholders and earlier titles in the existing name field; cached estate names migrate too.
- Character observations appear inside existing Character details. No separate game profile or house section.
- Diagnostics can capture bounded string arguments from garden-system messages while recording at a garden target. Player chat is not collected; diagnostics are not uploaded by sync.
- Opening an English mature-bed menu now syncs ready-to-harvest per physical patch and bed, even when cancelled. Existing crop, batch, planting time and tending records stay unchanged. No harvest is recorded. Crop identity from opening a bed is still unverified and not synced.

Test: open your FC member list, view your owned estate placard, and allow automatic sync. For crop observation, start a diagnostic recording, open one mature bed, cancel, stop and export. Leave the crop planted.

# 0.4.0.0 — details and planting test release

Deploy Journal V7.9.19 before updating the plugin. Existing pairing keys work.

- Records selected seed/soil at the game confirmation handler; only a matching successful planting response produces a per-bed record.
- Character snapshots include name, home/current world, race/tribe/sex, active job, all observed job levels and highest combat level. Changed snapshots only; idle queues make no HTTP calls.
- Discover/update owned private and FC estates. Compare native owned-house ID with the observed estate. FC names require a matching FC/member proxy containing this character. No ownership inferred merely from visiting.
- Opening an owned placard captures its estate name and size, separately from FC name. Custom website titles/photos/notes are preserved.
- Website connection has separate character-detail and estate-detail sync switches.
- The native observers need an in-game test. Existing crop identity from tending, harvest, fertilizer, events/rewards and Fashion Report sync are not enabled in this release. No in-game automation is performed.

Test: open the FC member list with your character visible, open the owned estate placard, enter the house, then plant one seed and tend it. Cancel a separate planting attempt. Website enabled/visible: allow about 45 seconds after action. Check exact crop, soil, patch/bed, time, estate name and FC name. Exports omit pairing keys.
# 0.3.0.0

Optional paired sync to Equinox Journal V7.9.14. Uploads confirmed house entries and tending in bounded batches, retries failed connections and deduplicates successful receipts. No requests when no events are queued.

Includes home/current server names and housing district names from game data for matching existing Journal records automatically. Name plus home server identifies a character; full address identifies a house. Physical garden patches are linked once in the website.

Existing local tracking remains available without sync. Pairing is opt-in and keys never appear in diagnostic exports. Planner, planting and harvest detection remain future work. See website setup guide before enabling sync.
