# Companion 0.5.1.236

- **Cancel** in the character confirmation returns to the game's main menu when Companion opened the character list to identify this login. The recognised login stays remembered for the current game process, so another character can be chosen from the main menu.
- A character list opened manually is left in place. Cancellation never requests a character search or login, and never aborts an unrelated Lifestream trip.
- The return uses the game's Back button once it is ready, stops if another login or trip starts, and expires after ten seconds rather than leaving a delayed action. **Stop return** cancels any remaining return work.

Validation: added cancellation regression cases for list ownership, stale confirmation, loading, single button dispatch, completion, timeout, manual login and unrelated travel. The build pipeline also runs the full existing regression suite and native garden tests before publishing. The native return still needs the in-game Cancel test.

Companion-only update; no Journal or Worker deployment is needed.
