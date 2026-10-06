COMPANION 0.5.1.62 / JOURNAL V7.11.79 — FOLLOWTHEM TRAVEL CORRECTIONS

Companion changes
- Retain movement-stop requests across occupied/loading frames; cancel active native autorun with the toggle only when the game reports autorun active. Stop, missing target, and stuck timeout request movement cancellation independently of the previous UI phase.
- Mirror dismount on the ground (enabled by default). No forced dismount in flight or while diving.
- Read SelectString entries through the native PopupMenu. Resume only matching source/menu interactions, including Aethernet, residential ward selection, workshop and exit choices.
- Ordinary public/estate teleports no longer approach the leader's old position. Nearby interactions accept a valid interaction range rather than requiring the follower to occupy the exact captured point.
- Keep richer Friends List estate captures ahead of generic teleport captures. Own private/FC estate teleports can fall back to the leader's native friend-estate menu when the follower lacks the estate in their own teleport list.
- Private-chamber callbacks are replayed only against the same visible room-list fingerprint, using the recorded integer selection. A different list stays manual.
- Ordered active-session travel delivery, retained through loading, with an arrival check before the next trip. Two-minute expiry; Stop/target change clears the local route. An expired or rejected trip does not skip onward blindly.
- Separate local options, default OFF: accept selected-character party invites; accept duty-ready prompts when that character is in the party; meet at shared Teleports from another map (same current World).
- Connection-ready messages no longer overwrite travel diagnostics. Interaction-position rejection reports that arrival has not completed; Lifestream approach failures include the exception type in diagnostics.

Journal changes
- Only Companion travel relay changes: ordered delivery scoped to sessions that were active at send time; cursor-based reads and expiry cleanup. Journal saves, garden data, collections and guestbook are not modified by this release.
- Existing single-signal clients remain compatible. New ordered routes and private-chamber metadata require V7.11.79 on Cloudflare.

Validation
- Release build: expected zero warnings/errors; see final build verification.
- Companion regression suite and portal/normal Companion API tests run locally.
- No actual FFXIV runtime available here. These changes require the in-game retests below. Native stop toggling, menus, friend estate fallback, party/duty buttons, approach IPC, and private-chamber list compatibility are not yet runtime-confirmed.
- Lifestream remains a separate optional plugin. No vnavmesh dependency was added. If it cannot supply direct approach, move closer manually; this release does not provide obstacle navigation.
- Unknown prompts, differing room lists, locked destinations, service restrictions and out-of-range doors remain waiting/manual; no universal portal support is claimed.

QUICK RETEST — BOTH CLIENTS ON 0.5.1.62, JOURNAL V7.11.79 DEPLOYED
1. Top-bar Stop while running: actual movement stops; moving the leader does not restart until Start.
2. Stuck timeout at 10 seconds and leader disappearing: WAITING stops actual movement; returning/moving leader resumes according to Resume nearby.
3. Mount then dismount on ground. Retest takeoff/landing once to protect the working behavior.
4. Gridania/Radz/Kugane main crystal to shard and shard to main crystal without manual clicks. Then ward entry/exit.
5. Workshop entry/exit, private chambers, house entry/exit. Record exact game error if any; do not mark arrival successful merely because a menu opened.
6. Private estate via Friends List; leader's own private estate; FC estate. Both characters must actually have that friend's estate access.
7. Two successive shard trips while follower is still loading. It must perform them in order; Stop between trips must discard the remainder.
8. Public Teleport and party Teleport regression. Optional Meet at shared Teleports ON: different maps, same World. OFF: prior source checks remain.
9. Opt in to party invites and duty-ready acceptance. Selected character invite accepted; unrelated inviter not accepted. Duty Commence only while selected character is in the party.
10. Leave duties OFF then ON after loot resolves. Pending-loot behavior remains unverified in the user's two-character setup.
