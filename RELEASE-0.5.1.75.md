# Equinox Companion 0.5.1.75

FollowThem corrections based on the Gold Saucer diagnostics from 7 October.

- Keep the selected leader targeted while the game processes follow, instead of immediately restoring the previous target.
- Recognise the literal `<t>` invalid-target rejection, clear the failed request and reset recovery timers. This avoids waiting for cancellation of a rejected follow request. Preserve the bounded native stop path for real movement.
- Read bounded aethernet rows from the available value count instead of requiring at least 20 rows. Add destination/source rejection diagnostics and menu value counts; compact and larger menus are covered by regression tests.
- Observe private/FC friend-estate list clicks, which do not use ordinary SelectString choices. Preserve the selected friend's content ID and estate type. Relay only after observed departure; the follower must have that friend and access, and still checks owner, row, cost and arrival.
- Category-bar behaviours are unchanged: the user confirmed expansion, colours, saved state and search all passed on .74.

Validation: Windows Release build, zero warnings/errors; 1,280 automated checks passed on GitHub. In-game acceptance of these corrections remains pending. Journal stays V7.11.81; no Cloudflare deployment.

Quick tests: Gold Saucer crystal -> shard -> crystal; leader disappears during follow then returns; shared friend's private estate and FC estate; stop and stuck timeout. Export follower diagnostics if WAITING persists, and both clients' diagnostics if a trip is not relayed.
