# Terrain-aware walking clearance 0.5.1.477

The .476 export confirms successful approach to the elevated Shirogane shard,
followed by unresolved Shirogane P11 / Mist P42 planning failures and Goblet P22
walking/replanning failures. Their road seeds stop on collision validation after
1-26 samples. These are not missing-map or unselected-corridor failures.

Research:
- awgil/ffxiv_navmesh at 38d2a24c3306f24640e64efe13915a78d301e878:
  NavmeshSettings.cs, NavmeshBuilder.cs, NavmeshQuery.cs and SceneExtractor.cs.
  It builds supported walkable spans with climb/slope/clearance constraints before
  querying the mesh. Our ray-based body test must likewise account for the floor
  under the body, rather than treating rising support as a wall.
- NightmareXIV/Lifestream at b36320170faafc8315f7585edb45cfbcd34e7046:
  Movement/FollowPath.cs and residential routing. Following known paths does not
  resolve our native swept-clearance rejection; no new route coordinates or
  runtime dependency are introduced here.
- FFXIVClientStructs BGCollisionModule.cs material-filter documentation confirms
  value=0 uses an any-bit mask. Keep the existing support/movement filters; do
  not remove walls by changing filters to make these tests pass.

Confirmed code defect:
The longitudinal side rays used max(previous CENTER floor, current CENTER floor)
for their height, even after measuring different support beneath the lateral
sample. On a sideways slope the low .2-yalm ray therefore runs inside the uphill
terrain. Cross-body rays similarly used only the center floor; an uphill edge
could intersect these rays despite having continuous walkable support.

Changes:
- Place each side ray above its actual previous/current lateral support.
- Acquire footprint support in <=.3-yalm steps with the existing .45-yalm maximum
  individual rise/drop. Avoid rejecting a sequence of valid small steps just
  because its total rise across the footprint exceeds one step.
- Place cross-body checks above the center and both supported ends of each diameter,
  so descending sides cannot lower a check into the center of a convex crest.
  Missing support, excessive individual steps, headroom checks and obstacle rays
  remain enforced. Body radius .6, height 1.85 and native material masks remain.
- Reuse exact ground samples within one live sweep, then clear them in finally.
  No live sample is retained for a later movement decision. Existing route-local
  planning caches and per-frame planning budgets remain.
- Record bounded rejected-connection phase/coordinates in each search snapshot;
  road-seed stops include the specific traversal rejection. Completed cached
  failures preserve their phase; interrupted checks are never cached as results.

Validation:
The new cross-slope fixture fails against published .476's traversal and passes
with this correction. Rising/descending slopes, a convex crest, consecutive legal risers and a
complete open cross-slope route pass. Walls across slopes, unsupported/excessive
side ledges and an off-center thin post still block movement. Existing cache
interruption, native approach, stage-7 elevation, navigation and quest tests pass.
The baseline harness changes only optional diagnostic signatures to run the same
fixture, not the historical collision logic.

This reproduces a genuine shared clearance defect. The live export does not
contain individual native hit geometry, so it cannot establish that this defect
explains every reported rejected connector. Mist P42, Shirogane P11 and Goblet P22
still need live confirmation. Generic plot street-entrance detection is not part
of this change. No longer timeout or larger per-frame planning budget is added.
