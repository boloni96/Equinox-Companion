# Navigation 0.5.1.469

Movement now distinguishes preferred wall clearance from physical traversability.
Previously the planner accepted a traversable edge with a wall-proximity cost, but
live pursuit required that its intermediate clearance never worsen. That stricter
rule could turn a usable planned or remembered route into an obstruction and launch
another expensive search without the character actually being stuck.

Comfortable lookahead remains first choice. If it and the next waypoint fail that
preference, pursuit checks a direct step toward the next waypoint using the same
physical traversal rules as planning, including remembered blocked targets. If the
connector is too long for a stair rise, bounded shorter steps are checked. This does
not permit collision, unsupported floors, or unchecked corner-cutting. Failure of
all physical steps still stops/replans. No new Lifestream or vnavmesh dependency.

Comparison reviewed 2026-11-07:
- Lifestream b36320170faafc8315f7585edb45cfbcd34e7046:
  Movement/FollowPath.cs follows waypoints and detects passed waypoints using the
  previous/current player segment; Tasks/Shortcuts/TaskPropertyShortcut.cs uses
  saved entrance/workshop paths and waits for movement before interacting.
- ffxiv_navmesh 38d2a24c3306f24640e64efe13915a78d301e878:
  NavmeshManager.cs caches serialized geometry navigation meshes and runs queries
  on worker tasks; NavmeshQuery.cs uses mesh path and straight-path queries.
  This differs from Companion's frame-budgeted native collision search. Moving
  native game collision calls blindly to a worker is not an equivalent solution.
- References: https://github.com/NightmareXIV/Lifestream and
  https://github.com/awgil/ffxiv_navmesh . No upstream code copied.

Validation: independent navigation regression tests, GateTests and Release build.
New tests exercise reported Goblet waypoint coordinates with synthetic clearance
callbacks, physically blocked connectors, steep stair lookahead and unchanged
open-road smoothing. These are policy tests, not a replay of captured game geometry.
Live Goblet/Limsa retest is still required; this does not establish that the latest
69-second full-route calculation or every physical obstacle has been fixed.
