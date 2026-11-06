# Navigation 0.5.1.464

The 0.5.1.463 Goblet East to plot 22 report timed out after 90 seconds with 407 expanded points and 217,014 native collision queries. Its closest explored point was about 11 yalms below the selected approach. This confirms search cost and vertical separation; it does not establish whether the final approach is reachable or correct.

Changes:
- Search priority includes remaining vertical distance, with the same 1.2-yalm arrival tolerance. Previously only horizontal distance affected priority.
- Planning reuses completed edge and body-clearance results at exact coordinates. Caches belong to one planning collision instance; unfinished checks are never stored. Live steering still runs fresh checks.
- Existing radius, bidirectional obstacle checks, support tests, frame budget, cancellation and arrival checks remain in force.

Validation: independent navigation suite includes elevation-aware ramp routing, lower-floor distance, cache reuse, interrupted checks, blocked paths and per-route isolation. Plugin Release build passes. In-game route completion and actual timing improvement are not yet verified.

Test: retry the same Goblet GO SEE. Export after 15–20 seconds if it remains calculating, or after arrival/failure. The export still includes explored points, rejected points, closest trail and native query counters. No need to wait 90 seconds before reporting.
