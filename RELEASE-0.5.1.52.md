# Companion 0.5.1.52 — Garden batch assignment

In /gardening (or /planting), expand Batch mapping · left to right.
An existing house member under the unchanged owner/visitor rules can select a
mapped bed, then assign its physical patch to Batch 1, 2 or 3. Occupied labels
swap and the guide opens the destination. Large estates: 1 left, 2 middle,
3 right. Medium estates: 1 left, 2 right. This is explicit assignment, not
position guessing or automatic remapping on a normal target click.

Visitors get Reset batch mapping / resync. It restores Sync beds for the selected
batch, including after restarting Companion, while retaining existing garden
records. Follow the existing eight-bed calibration sequence; completion commits
all mappings together. No owner/visitor detection or harvesting rules changed.

Shared assignment requires Journal V7.11.69 (protocol 15). Deploy the matching
website package and refresh it. Old websites show an upgrade message. The
website stores the order separately from physical patches; crops, care, history
and plans remain associated with their patch. Stale assignments are rejected.
Wait for the queued order before making another assignment or editing its plan.

Validation: 858 automated native checks; Release build without warnings/errors.
All six three-patch orders, selected-target following, duplicate/stale assignment,
physical bed retention and invalid permutations checked. Matching website checks
cover server sanitization, member rules, retained history/timers/plans, reordered
plan reset, subsequent tending, live projection and browser reload persistence.
In-game interaction/FPS is not verified here. No garden FPS improvement claimed.
