# Companion 0.5.1.113

- Add Use Aetheryte Tickets for Companion teleports under Travel & shared destinations; default off preserves tickets and selects paying gil.
- Answer the exact English ticket prompt only within 15 seconds of Companion's own native teleport request, for the same active trip, follow session, world and area.
- Match the ticket prompt independently of the character's inventory count. Stop, pause, disabling travel, loading, area changes and trip changes invalidate the pending decision.
- Submit the selected native button event once, retaining existing gil limits and arrival verification.
- Zone-edge capture is still under investigation; this release does not change it.

Build/regression checks do not establish in-game success. No Journal deployment needed.
