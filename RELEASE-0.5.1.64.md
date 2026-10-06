# Companion 0.5.1.64 — follow stop and queued travel test build

Journal remains V7.11.79. Update both Companion clients. No Cloudflare deployment required.

Changes
- Replace /automove off as the game-follow cancellation mechanism with a bounded 80 ms backward-input pulse inside this game client. It sends no OS keyboard events and adds no movement/navigation dependency. The hook is disabled outside the pulse. Plugin input reads bypass the synthetic pulse, so it does not count as manual movement. Native cancellation is observed through the game's “No longer following a target.” message; position alone is not proof, especially against a wall. Failure is logged and travel interaction is held if cancellation remains unconfirmed.
- A queued trip can start its stop/preparation phase while the follower is moving. Normal following stays paused while that trip awaits arrival.
- Manual movement pauses preparation and preserves the trip's original two-minute expiry. Releasing movement resumes preparation. Explicit Stop and optional Stop on movement still cancel the session/queue.
- Recognize loading for short same-ward estate teleports, not only a territory change or movement over 12 yalms.
- Exclude section headers from Aethernet destinations. The supplied Ul’dah menu has multiple section headings with callback 0, which previously made the real main-crystal destination ambiguous. Keep approach points on the leader-facing edge of the crystal instead of outside interaction range.
- Translate the leader’s own-chamber shortcut into specified chambers plus the recorded room number/owner. Read numbered-room selections semantically rather than requiring both users' entire menus to be identical. Recognize own-room confirmations during capture, verify visitor/owner confirmation on replay, and allow the exact Leave private chambers choice.

Boundaries
- This is an in-game validation release, not a claim of runtime success. Stop input interception/cancellation is the first retest. A tiny backward movement may be visible. An unavailable interception path tells the user to stop manually.
- Room lookup currently selects only a room visible on the current page. If another page is needed, select that page manually. It never substitutes a different owner’s room or accepts a locked-room error.
- Landing/airborne dismount remains a separate unresolved item. No universal portal or obstacle navigation support is claimed.
- No Journal saves or pairing settings are added by movement checks/diagnostics.

Validation
- Release build: zero warnings/errors. 1,116 regression checks pass.
- New cases cover the real Ul’dah header/callback collision, same-ward loading versus no departure, chamber room/owner identity, confirmation mismatch, chamber exit, and crystal edge/floor preservation.
- Native follow cancellation and replayed game interactions cannot be verified outside FFXIV.

Retest in order
1. In open space, Start following, then top-bar Stop while leader keeps walking. Follower must stop and remain stopped. If not, stop manually and export diagnostics; do not continue travel tests.
2. Restart, set stuck timeout to 10 seconds, test against a wall. Actual game follow must cancel; returning closer should resume according to Resume nearby.
3. Ul’dah main crystal → shard → main crystal. Try the first trip with the follower a little farther away but in range for approach.
4. Gridania → FC estate; then private estate → nearby FC estate while staying outside. Both require an actual follower loading transition. Test a brief movement-key pause during preparation with Stop on movement OFF; release must resume the kept trip, not cancel it.
5. Chamber: leader uses own-room shortcut, then numbered-room selection, then exit. Confirm the follower selects the leader’s exact room, not its own. Export diagnostics if a different confirmation/list appears.
6. Regression: FC estate window already open/closed; house → workshop → house → outside quickly. Preserve prior .63 FC estate passes, but verify these after native stop changes.
