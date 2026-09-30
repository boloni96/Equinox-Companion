## 0.4.0.0 additions

Events: `character.updated` (no address), `house.discovered` (owned-estate-id evidence and optional confirmed FC/placard metadata), `garden.planted` (selected seed/soil + success response coordinates). Existing house.entered and garden.tended are unchanged. Plugin batches remain at most 50 events, conservatively under 60 KB including JSON formatting.

Metadata is sampled locally every five seconds, recorded only when content changes, and shares the existing upload queue. No idle network heartbeat. Journal keeps custom content and existing batch labels, deduplicates houses by address, and pauses conflicting ownership/FC relocations for review. Highest combat job level controls existing character level gating; all jobs remain available in observed details.

# Connection to Equinox Journal

Baseline inspected: V7.9.10, specifically `site/assets/journal.js`,
`site/_worker.js`, and `gardening-source/house-gardens.js`.
No journal file or live cloud data was changed by this prototype.

## Existing journal model

- `data.properties` is the shared property registry. A property has `id`,
  address fields, `ownerId`, `members`, and `visits`.
- A visit's `qualifies` field influences `houseAge`. A raw entry observation
  must not automatically set that flag. Qualifying ownership/membership and
  apartment/FC-room/workshop distinctions require verified rules.
- Gardens link to properties through `propertyId`; `g.patches` holds their
  patches. House and character screens already use those same records.
- Current Worker writes require website Origin and a session cookie. Do not
  bypass this by pretending the plugin is a browser or giving it the journal
  password. Add a dedicated, narrowly scoped companion endpoint.

## Required connection design

1. Pair each plugin installation from a signed-in journal session with a
   revocable device token. Keep the normal journal password out of the plugin.
2. Send small observed events with unique IDs and UTC timestamps, using a
   durable local outbox and server-side deduplication.
3. Store events separately from whole-journal snapshots, then merge accepted
   events through the journal's existing revision/conflict mechanism. A stale
   plugin snapshot must never overwrite pictures, notes or manual edits.
4. Link game character ContentId to journal character ID once. Treat ContentId
   as a string, avoiding JavaScript's integer precision limit.
5. Link canonical game property identity to one `data.properties` record.
   Validate display ward/plot numbering and territory-to-district mapping in
   game first, including subdivision and visits from another world.
6. Link each persistent garden patch and bed to the corresponding journal
   patch/bed. Furniture indices and target object IDs in this diagnostic are
   **not yet established as persistent identities**. Moving/replacing furniture
   and zone reloads must be handled before enabling automatic updates.
7. Confirm success independently of selection or button press. Cancellation,
   failed interactions and actions on neighbouring properties must do nothing.
8. Apply only validated operations: confirmed planting creates the planting
   record/batch; tending updates only that bed; fertilizing updates its history
   and estimate; harvesting closes only that planting.
9. Preserve observation history, source device/character, and last-observed
   timestamps. Distinguish observed maturity from estimated harvest readiness.
10. Keep manual corrections inside expanded details, and leave unknown or
    ambiguous observations unapplied. Two connected players should converge on
    the same shared house record without duplicate actions.

## Diagnostic contract

`schemaVersion: 1` exports include `houseObservations` and `diagnostics`.
Housing observations contain unique event IDs and copied value data.
`game.logCandidate` records copy numeric log parameters at callback time;
their `contextReadAt` records when housing/target context was sampled on the
framework thread. This small time difference is why context is not proof of
which bed received an action. No native pointers are exported.

The first prototype intentionally does not mutate the website from these
diagnostics. The next adapter must prove action-to-bed association, using the
recorded session plus inspected game APIs, before this becomes automatic.

## Upstream references inspected

- https://github.com/goatcorp/SamplePlugin
- https://github.com/goatcorp/Dalamud.NET.Sdk
- https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Services/IPlayerState.cs
- https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Services/IChatGui.cs
- https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/HousingManager.cs
- https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/Housing/OutdoorTerritory.cs
- https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentHousingPlant.cs

Altoholic was a useful user-provided example. This prototype does not integrate
with it or claim to reproduce its implementation.
