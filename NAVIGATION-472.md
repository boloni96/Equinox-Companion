# Navigation 0.5.1.472

The supplied .471 export still shows five 90-second calculation timeouts and two
housing stops while collision geometry loads. Manual walking was not a recorded
route and is not required by this change.

Map-guided planning now first validates the map road line in supported half-yalm
steps, obtaining height from native geometry instead of using the map's planar Y.
Each edge uses the existing full physical traversal checks. The verified section
seeds the forward search, with its full checked predecessor chain; ordinary search
remains available if the road artwork encounters a wall, unsupported floor or a
steep edge. Processing uses the existing frame/query budget and cancellation.
The seed is capped at 128 edges and exported as RoadSeedPoints. No speculative
road edges or recorded manual paths are treated as confirmed walkable geometry.

Housing now waits for collision geometry before its approach/visibility checks.
Previously those calls could throw before IndependentWalk's readiness guard,
terminating a trip just after shard arrival. Interaction-target checks in walking
also respect readiness. Existing housing stage deadlines still bound the wait.

Validation: independent navigation tests, GateTests and Release build. Added tests
cover road bends on a ramp, deriving real height, blocking map routes at obstacles
and retrying a traversal interrupted by a query-budget yield. These synthetic
tests do not establish live Goblet/Mist success. Plot GO SEE still targets a checked
placard approach; exact street-side gate coordinates are not inferred from icons.
