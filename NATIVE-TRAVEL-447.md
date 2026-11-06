# 0.5.1.447 — stable aetheryte approach and walking steering

The 446 export showed both same-DC world trips completed. Approach paths were found in under a second; one approach then restarted because its destination changed, and another remained active until manual movement interrupted it. The final movement counters described a later successful estate entry, so they cannot establish the earlier input-pulse behavior.

Keep one grounded approach destination per interaction attempt, resetting it only for a closer retry. Set the aetheryte target and allow an update before interaction, with recorded attempted distances. Steering connectors retain ground/body collision checks but no longer reject physically clear movement just because it lacks preferred wall clearance. Initialize heading toward the route, use the checked route direction when a rounded turn is blocked, and vary speed continuously instead of alternating slow/full or stopping for alignment.

Each ended navigation now records its counters and stop reason in the bounded travel history, preserving earlier journeys when estate entry later overwrites the live snapshot.

ShoppingCity remains exclusive to shopping trips; no automatic travel destination preference is changed.

Validation: full build, independent navigation regressions including continuous turn-speed behavior, and five release CI suites. In-game retest remains required for smoothness and farther aetheryte interaction.
