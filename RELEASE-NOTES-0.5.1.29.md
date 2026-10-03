# Companion 0.5.1.29

Garden warnings now reset on each real character login, including logging back into the same character with unchanged bed states. After player data loads and the existing 20-second settling delay, eligible warnings appear in one compact colored summary.

While the character remains logged in, identical warnings stay suppressed. Changed care/status or urgency can produce another summary. Temporary missing player data during teleporting or changing areas does not reset the warning memory.

The owner/shared-member/tenant eligibility filter and local-only chat behavior from 0.5.1.28 remain in effect. Characters unrelated to a house do not receive that house's warnings merely because they share the pairing key.

The corrected soil/shade alignment and lowered care icons from 0.5.1.28 are included. Journal V7.11.41 already contains the matching website layout; no new website upload is required for this login behavior correction.

Validation: character-session tests cover initial login, unchanged suppression, missing player data during zoning, same-character relog, character change and changed urgency. Companion builds for Dalamud API 15. Native login/chat behavior still needs in-game verification.
