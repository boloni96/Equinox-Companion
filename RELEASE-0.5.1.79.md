# Companion 0.5.1.79

Recognises a friend-estate request targeting the follower's own content ID and resolves the exact owned private/FC house in their native teleport list. Other friends still use their own verified estate menus.

Keeps the follower relay lease active during Companion-owned Lifestream world/DC travel, including login transitions. Outgoing travel waits for an active follower and preserves its original timestamp and maximum two-minute expiry; disabled sharing, changed pairing and expired trips are discarded.

When the leader disappears then returns to the source of an unfinished door trip, Resume nearby clears that stale door and dependent legs. Travel failure clears stale movement recovery state. No door permissions are bypassed.

Regression checks cover owner identity, bounded trip expiry and door-return guards. In-game verification remains required. Journal V7.11.81 unchanged.
