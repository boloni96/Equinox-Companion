# Companion 0.5.1.82

Quest Helper now relays confirmed quest acceptance with the exact quest ID. A declined offer does not trigger acceptance. The follower accepts only the same offered quest during the matching NPC interaction and waits for game confirmation; blocked prerequisites or quest-log space remain visible.

Leader controls separate Pause Quest Helper (NPC/dialogue assistance) from Pause FollowThem (whole session). Resuming either preserves the other pause. Stop still ends permission and requires the follower to Start again. Removed the redundant Advance my current dialogue button; normal dialogue clicks continue to relay.

Deploy Journal V7.11.83 for the updated quest acceptance and pause relay. FollowThem travel remains on the existing relay. No Journal records are written by Helper actions. Native Accept-button events only; no keyboard shortcuts.

In-game acceptance and independent pause checks remain required; automated tests cover relay validation, authorization and pause precedence.
