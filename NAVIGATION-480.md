# 0.5.1.480 — generic map and geometry feedback

The screenshots describe how to walk around any plot, not a request for a catalogue of special-case routes. Known coordinate hints remain optional aids; the shared route planner now also adapts an ordinary game-map guide when native geometry disproves it. This mechanism applies to any route with usable map guidance, independent of territory or plot number.

- Search bounds cover the map region being explored, including longer street detours, instead of staying tied to the rejected shortcut.
- A rejected map-guide connection records its position. A managed worker rebuilds the 2D guide around the observed blocked areas, with a three-yalm guide buffer (or larger for coarse map cells). This buffer is a planning preference; all resulting steps must still pass native 3D support and collision checks.
- The start can escape sideways or away when already beside a newly found boundary. A new guide starts at the actual requested position instead of snapping the player onto a map-cell centre that may lie inside a wall.
- Up to four alternate map guides can be checked in one search. The general 3D search continues while a map calculation runs and remains the fallback when map guidance is unavailable or alternatives fail.
- Verified sections extend the existing walking path, keeping the current waypoint. Forward and reverse planning resume from the latest committed section instead of repeatedly searching the shard area. Complete returned paths retain the verified section chain.
- Only immutable managed map data enters the worker. Native geometry, movement gates and live traversal stay on the framework thread. There is no persistent cross-floor exclusion or runtime dependency on Lifestream/navmesh.
- Exports include map reroute counts and blocked areas, and preserve the completed planning snapshot after walking starts.

Validation: the generic regression creates an artificial map and a boundary absent from that map, then repeats with a rotated layout. Both complete with four map detours, five verified walking sections and five 3D expansions; every returned connection is checked against the synthetic geometry. No housing plot coordinates or route hints are used in these tests. Existing navigation/quest, stair, wall, cancellation, input and gate tests also pass; the production build has zero errors.

Limits: map artwork is not a walkability mesh, and unseen game geometry still needs live validation. Completion of every housing plot is not claimed from synthetic tests. Generic routing, the 0.5.1.479 prefix handoff, and 0.5.1.478 continuous pursuit are included in this package.
