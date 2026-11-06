# Navigation 0.5.1.465

The Goblet East to plot 22 reports show prolonged searches on the lower level, with about 195,000 native queries for 362 expanded points in 52 seconds. The prior elevation/caching change did not resolve this live route.

## Changes

- Build connected road-distance fields from the game map texture on a worker. These guide detours around blocks rather than only applying a local road-colour penalty. The game map/minimap coordinates align the fields; opening the map UI is unnecessary. Map artwork is advisory and can be wrong about obstacles or height.
- Search from both the player's floor and the destination floor for routes of at least 12 yalms when destination ground is available. Both sides share a horizontal lattice but retain actual sampled elevation. Reverse edges are checked in the eventual walking direction; joins require compatible height and a verified connector.
- Defer full swept-body collision checks until a candidate edge is selected. Failed candidates retain alternatives from other parents. Slice expiry resumes the pending validation, including reverse edges and joins.
- Let the search bounds include detours suggested by the connected map route. Geometry-only fallback remains available for missing/unrecognized maps and unavailable destination ground.
- Exports include the planar road guide and separately sampled reverse-search points, so failure on either side is visible.

Obstacle radius, two-sided collision checks, stair support sampling, live steering checks, frame budget and right-click cancellation remain unchanged. No Lifestream call was added.

## Validation

Release build and independent navigation tests pass. New regressions cover U-shaped road detours, stacked floors connected only by distant stairs, forbidden uphill movement on a one-way slope, mid-edge slice expiry on both searches, impassable walls despite map guidance, and selected-edge probe counts. Existing regression checks also pass.

The actual Goblet texture and reported coordinates produce a 6,072-cell field with 28 guide points; building it took about 24 ms in the local worker-only benchmark. This is not the in-game geometry-planning time.

The live Goblet route is not yet confirmed fixed. Test the same GO SEE after updating. Export if it remains calculating or stops; the new reverse-search samples show the destination-side geometry as well as the player's side.
