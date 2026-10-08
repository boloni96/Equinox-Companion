# Companion 0.5.1.109

- Record the leader's Leave button on a solo-duty prompt, separately from Proceed.
- Replay Leave only for the exact matching quest, progress and prompt, using the game-owned Leave button event.
- Wait for the original window to close before resuming following; never turn cancellation into duty entry.
- Supports SelectYesno and DifficultySelectYesNo. Window X/Escape dismissal is not inferred as a recorded Leave.
- Correct queued quest envelope timestamps at actual send time so the relay's ten-second freshness validation accepts recordings retained during a brief status gap; original step timestamps are preserved.
- Regression checks distinguish Leave, Proceed and unrelated prompts.

Build/test validation is not in-game verification. No Journal deployment required.
