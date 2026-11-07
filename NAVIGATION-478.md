# 0.5.1.478 — continuous pursuit and checked side detours

The 03:38:07 export from 0.5.1.477 includes successful interaction approaches with 61 and 70 support points, plus repeated housing planning timeouts in the Goblet, Mist and Shirogane. A large support-point count is not itself proof of stopping, but steering could fall back to individual dense samples whenever preferred wall space rejected its longer lookahead.

Changes:
- Aim toward the end of straight runs (bounded to 128 samples, preserving bends, reversals and elevation changes); keep the underlying support path for progression and collision validation.
- Keep a physically clear forward pursuit when extra wall clearance is unavailable, instead of immediately targeting the next half-yalm point.
- If the next connection is blocked, try a short left/right dogleg. Both legs must pass the physical traversal and remembered-obstacle checks. Candidates remain within the existing bounded traversal distance. This is local avoidance, not a replacement for global pathfinding.
- Retain the previous 180 ms movement lease while preparing its replacement. Every pause, failure and non-renewing return revokes it in finally; stale updates still expire. No speed pulsing or longer input timeout.
- Reuse exact ground and swept-connection results only inside one walking update, clearing them in finally. No new inter-frame geometry cache.
- Forward collision rejection reasons through the stalled-obstacle wrapper. The previous export lost these details and reported only “unspecified collision rejection.”

Reference review: awgil/ffxiv_navmesh separates polygon/support search from straight-path movement and supplies continuous directional input. Our movement continues to use native collision queries and has no runtime dependency on navmesh or Lifestream.

Validation: independent tests cover a 70-sample straight corridor across successive frames, real corners, crest height, reversals, fully blocked paths, clear-left and clear-right detours, and blocked reconnections. Existing collision, stairs, route, movement-gate and release tests remain required. Live smoothness and housing completion are not yet verified. This release does not claim to solve the remaining 90-second housing planner failures.
