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
