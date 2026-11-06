# Companion 0.5.1.462 — planning budget and search focus

The supplied 0.5.1.460 Goblet report contains three 90-second planning timeouts without movement. The last search expanded 2,388 nodes. Navigation averaged 7.57ms per update over the recent window and peaked at 50.18ms. This is a planning failure; the backtrack never ran because no route was found.

Native query budgeting now reaches inside collision traversal. A search can pause before the next uncached raycast and resume the same node/edge using cached query results, rather than finishing an entire expensive edge after the update budget has expired. The normal planning slice is reduced from 6ms to 3ms. A single native call cannot be interrupted, so this is not a hard whole-frame guarantee.

The weighted search prioritizes progress toward the destination more strongly, retaining the same geometry validation, road costs and clearance costs. It may choose a different valid route; shortest-path optimality is not guaranteed. A synthetic 48-by-10-yalm costly-ground test expanded 478 versus 3,494 nodes with the previous priority weight. This is not a live Goblet benchmark.

Tests cover interrupted edges, interrupted final connectors, query quota release, road/clearance preference, narrow passages, stairs and collision guards. Diagnostics retain budget-yield counts when planning stops. The live Goblet route still requires verification.

Includes the already-published 0.5.1.461 Framer's Kit ID/fallback corrections.
