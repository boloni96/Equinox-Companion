# Companion 0.5.1.63 — FollowThem travel correction test build

Journal V7.11.79 remains the required relay version; no new website deployment.

Changes
- Decode native menu SeStrings and submit explicit SelectString callbacks. Retry missed source interactions at most three times, only while stationary, in range and without another dialogue open.
- Preserve ferry NPC context for ward trips and advance that matching NPC's Talk dialogue.
- Add a friend-estate list selector that verifies owner, estate type, unique enabled row, displayed fee and gil limit. An already-open estate window is recognized. Unreadable rows remain waiting; a final unrecorded confirmation remains manual.
- Queue arrival requires departure evidence. Approach movement itself is excluded. Same-territory loading is recognized, and the next trip waits for a stable loaded position. Visible leader identity is refreshed even while menus are open.
- Ground Lifestream approach ignores small target-height differences, without vnavmesh. Clear owned approach movement on arrival.
- Send explicit /automove off once even if the autorun probe already says off. Avoid rearming the stuck latch merely because the leader moves farther away. Use current-target /follow without a literal <t> argument.
- Missing FC tag/proxy readings now mean unknown, not FC departure; retained FC/house/submarine links are preserved. A real FC departure is not automatically inferred from empty tag samples; manual association correction may be needed.
- Add an installation-guide button for optional Lifestream.
- Add a bounded local travel diagnostic history to Settings → Logs/export → Export diagnostics with error history. It includes travel menu names/text/numeric values, submitted callbacks and status changes. It does not sync with the Journal.

Validation
- Release build and 1,096 regression checks pass locally. No FFXIV runtime available here; native interaction success is not established by compilation.
- Existing in-game passes on .62: party invite, duty acceptance, public/party teleport, basic mount/dismount, individual house/workshop entry/exit, duty leave ON/OFF. Retest movement and rapid travel on both .63 clients.

Still open
- Private-chamber owner shortcut versus numbered-room selection and exits. The old fingerprint replay remains; this release does not claim a complete chamber fix.
- Intermittent airborne dismount/landing. No new descent automation is claimed.
- Estate row selection, ferry Talk advancement, all crystal routes, native follow cancellation and quick room-to-house queues require in-game verification.
- Unknown estate confirmations remain manual. If a step waits, export diagnostics on both clients immediately before completing it manually, then export again after the manual selection. Do not repeatedly spend gil to collect the same failure.

Quick tests
1. Stuck timeout 10 seconds: WAITING must stop actual movement. Leader moves farther: follower stays waiting. Return closer: follow resumes. Top-bar Stop must stop and remain stopped.
2. Ul’dah/Kugane main crystal → shard → main crystal, no manual clicks. Then residential district → ward. Test ferry separately.
3. Friend private/FC estate with window closed, then already open. Verify correct estate and price; note whether a final confirmation needs a manual click.
4. Workshop → house → outside rapidly while follower loads; repeat two shard trips. Arrival must not be announced before travel.
5. Leonis FC/submarine association survives refresh/zoning while FC data is briefly unavailable.
6. Regression: party invite/duty accept, normal Teleport, mount/dismount, flight, house entry/exit. Chambers and airborne landing remain open investigations.
