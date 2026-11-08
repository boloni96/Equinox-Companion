# Garden approach waypoint correction — 0.5.1.503

Report 041214 (.502): the Shirogane W15 P15 entrance segment completes, then the approach to batch 2 bed 1 ends after repeated replans. Four recorded obstructions have target equal to current position and stalled=false: the steering layer has no valid connector, rather than detecting physically stationary input. This does not establish the exact live obstacle geometry.

Code inspection found the projection-based AdvanceWaypoint branch validating only BoundedTarget (first 2.2 yalms) but advancing to the distant waypoint, discarding intervening corner points. Validate the entire connector before committing that index change. The other corner-join branch already does this. Keep reached-waypoint handling, pursuit smoothing, collision checks, recovery limits, bed identity and action confirmation unchanged.

Validation: Release build plus complete independent navigation/quest suite. Added regression for a nearby onward segment with its endpoint beyond an obstruction; a fully clear shortcut remains allowed and observed arrival still consumes its waypoint. Live garden completion is unverified. This corrects a reproducible steering defect, not a claim that all furniture/pathfinding failures are solved.
