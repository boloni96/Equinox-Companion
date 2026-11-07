# 0.5.1.479 — walk verified housing streets while planning onward

The 03:38:07 report shows the Mist route validating 161 street/stair samples to the upper approach, yet the forward frontier continued expanding around the starting area. The long verified route had been enqueued behind cheaper nodes at the origin; its useful progress was not exposed to movement until the entire route completed. Goblet and Shirogane guides stopped earlier at rejected connections.

Changes:
- Add narrowly matched, factual street-coordinate hints for the reported Goblet P22 and Shirogane P3/P11 approaches, alongside Mist P42. Reference: Lifestream HousingData at b36320170faafc8315f7585edb45cfbcd34e7046. These hints include the missing stair turns; every connection still needs native geometry approval. They do not generalize to other plots or subdivisions.
- Commit a completed, collision-verified guide prefix (at least five samples and two yalms of displacement). Price the remaining forward search from its endpoint and aim the reverse search there. Retain the complete checked prefix in the eventual returned route.
- Transfer the prefix to walking immediately while the remaining search continues in its existing per-frame slices. The prefix is transferred once; a completed route joins its exact shared suffix without resetting the live waypoint index or sending the player back to the start.
- When geometry rejects a road-line edge, try a bounded left/right passage and reconnection one or two yalms onward. Both legs and their ground support must pass native checks. If no candidate works, retain the verified prefix and let the general planner find a different route.
- No teleporting, unchecked movement, increased movement lease, recorded manual route, external navigation plugin, or claim that map artwork proves walkability.

Local verification covers prefix availability before full search completion, path handoff with an existing opening, mismatched floors, clear-left/clear-right obstacles, blocked reconnections, full barriers, and the existing navigation regression suite. Build and gate tests are also required.

Limit: live completion at Goblet P22, Mist P42 and Shirogane P11 remains unverified. The report did not retain underlying rejection reasons because the collision wrapper dropped them; 0.5.1.478 fixes that diagnostic propagation. This release improves a concrete planning/handoff defect and local road obstruction handling, not a promise that every housing path is fixed.
