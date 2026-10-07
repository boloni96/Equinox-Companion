# Companion 0.5.1.82

Quest Helper now relays confirmed quest acceptance with the exact quest ID. A declined offer does not trigger acceptance. The follower accepts only the same offered quest during the matching NPC interaction and waits for game confirmation; blocked prerequisites or quest-log space remain visible.

Leader controls separate Pause Quest Helper (NPC/dialogue assistance) from Pause FollowThem (whole session). Resuming either preserves the other pause. Stop still ends permission and requires the follower to Start again. Removed the redundant Advance my current dialogue button; normal dialogue clicks continue to relay.

Deploy Journal V7.11.83 for the updated quest acceptance and pause relay. FollowThem travel remains on the existing relay. No Journal records are written by Helper actions. Native Accept-button events only; no keyboard shortcuts.

In-game acceptance and independent pause checks remain required; automated tests cover relay validation, authorization and pause precedence.

Quest menus can contain different numbers/orders of quests: uniquely identified quest names are matched against the game's Quest sheet. Missing quest options skip that NPC interaction and retain the follow session. Ordinary dialogue still requires matching responses. Seasonal replay is not rejected solely because the Journal completion flag remains set. Includes the verified English Kipih Jakkya replay-confirmation mapping for A Nocturne for Heroes: Replay Yes maps to The Man in Black acceptance for a first-time follower, or the same Replay prompt. Other event branch differences remain blocked. New in-game tests are still required. Choice diagnostics capture the NPC, expected option and visible menu for follow-up verification.
