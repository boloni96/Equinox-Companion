# Navigation 0.5.1.487

Report 063011 on 0.5.1.486 confirmed Shirogane W15 P10 reached its garden, but Goblet W18 P43 failed twice on a subsequent bed approach (45 seconds, no movement). Shirogane W15 P9 reached the placard approach then its straight entrance step was physically blocked.

Garden target searches now terminate at any reachable supported node within the target-specific interaction distance (with a 0.25-yalm margin) and clear interaction visibility. The old preselected side point is no longer the only allowed destination. Reverse search from an obstructed object centre, cached exact-point routes, and fixed-point arrival shortcuts are excluded for this region search. Live target distance/visibility still controls stopping and actual menu confirmation remains with the caller.

Ward-to-plot entry now uses collision routing toward the existing interior landing anchor instead of an unchecked straight forward attempt from the placard. Matching estate identity plus movement beyond the first observed boundary position confirms handoff; identity alone does not stop the route. Teleport entry retains its existing short-step behavior. This is not geometric gate detection: RoomExit remains an interior anchor, and exact entrance stopping for every plot is still unverified.

Validation: full plugin build, independent navigation suite (including left/right fenced entrances and blocked garden centres with visible interaction sides), and gate suite. Live validation still needed for Goblet W18 P43 next patch and Shirogane W15 P9 entrance. No claim of universal navigation or continuous movement proven in game.
