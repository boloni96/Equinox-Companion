# Companion 0.5.1.91 — Quest completion and scene capture

Includes .90 quest scene capture, Destination object interaction and native cutscene-skip recording. Fixes a quest-sequence check that could reject a Destination after its own successful interaction advanced the quest.

Adds JournalResult Complete/Decline recording. The leader's actual Complete callback and game-confirmed quest completion are required before sharing completion; simply closing the window is not completion. The follower verifies quest identity and result title, uses the native button's click event, and waits for game confirmation before finishing the step. Completing the previous quest can now remain ordered before accepting the next quest.

Requires Journal V7.11.87 for the new completion action. Update both clients and deploy the Journal package to the same project. No Journal completion history is cleared or modified by Helper.

Optional reward selection is still manual: an unavailable Complete button blocks with an explanation instead of choosing an arbitrary item. Quest-specific duty automation, arbitrary confirmations and the separate Kipih first-time/returning greeting mapping remain outside this correction. Native tests are pending.
