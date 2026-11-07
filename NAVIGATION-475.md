# Shared route planner 0.5.1.475

The 0.5.1.474 live export still shows Mist W24 P42 and Shirogane W15 P11
planning timeouts. The Mist street hint was selected, so failure was downstream
of hint selection. Shirogane P3 arrived twice after 77/59 seconds of planning;
Goblet P22 failed once and arrived on two later attempts. These observations do
not identify the exact rejected Mist edge: a subsequent trip replaced the
single search snapshot.

Changes apply to the shared collision planner, without adding plot coordinates:

- Keep two/one-yalm search strides on open ground. When a unit edge has no
  support or fails its swept check, propose a half-yalm connector. The half
  connector must pass normal ground/body/obstacle validation before use.
  Existing close recovery gets proportional quarter-yalm refinement.
- Retain finer coordinates in the search keys. Refinements have a fixed fallback
  cost of 24 to prefer an available coarse route and avoid flooding whole floors
  with shifted lanes. This is a search-priority tradeoff, not a walking speed or
  collision-clearance change; it does not guarantee optimal paths.
- Connect opposing frontiers within 1.5 horizontal / 1 vertical yalm using a
  directional swept-body check, rather than requiring nearly identical cells.
  Try at most four nearest matches and remember completed failed joins. Slice
  interruptions do not cache an unfinished check as failure.
- Retain each ended search's bounded diagnostic snapshot in travel history,
  including explored/rejected samples, closest trail, query counts and road-seed
  stop reason/coordinates. This history still uses its existing 600-event cap.

Validation:
- New supported stair-turn regression at x=4.5 fails against published .474 and
  passes with refinement (18 expanded nodes). All output edges are rechecked.
- A wall across that stair remains impassable; the road seed reports its failed
  connection. Existing directional-floor, sliced-query and thin-wall tests pass.
- Existing open-road case remains 24 areas; stacked-floor case is 406 areas / 536
  traversal checks versus .474's 408 / 540. A regression cap prevents broad fine
  lattice expansion. Existing weighted-search benchmark is 365 versus 388 areas.
- Synthetic results are not live proof of Mist/Shirogane success. Native geometry
  must still be tested, and the next export now retains each failed search.

No larger per-frame planning budget, longer timeout, reduced actor clearance,
manual route recording or additional Lifestream dependency is introduced.
Generic plot street-entrance detection remains separate unfinished work.
