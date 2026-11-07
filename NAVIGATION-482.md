# 0.5.1.482 — resume from the walking position

The 04:24:23 export is version 0.5.1.480. Goblet W18 P22 completed twice. Mist W24 P42 instead reused a checked opening ending near (-685.41,15.40,-659.30), while its map guide still started near the shard (-689.15,18.00,-619.35). It walked back along the old guide, exhausted four map detours and waited at the verified section end. Version 0.5.1.481's search bounds alone do not repair this handoff.

- When a cached opening changes the search origin, rebuild the map guide from that origin on the existing managed map worker. Do not seed walking from the old route start. Initial rebasing does not consume the obstacle-detour budget.
- Reverse planning ignores a map distance field whose starting point no longer matches its destination.
- Street-corridor hints can resume at a nearby segment on the same floor, retaining only the onward route. This helper applies to all available corridor hints; it does not require manually recording another route. Every join and segment remains subject to native checks.
- Short interaction approaches that already pass ground/body traversal are no longer discarded solely for lacking extra preferred wall space. This does not remove physical clearance or interaction-range checks.

Regression tests cover a generic mid-street resume, wrong-floor rejection, reuse of a map after an opening moves the origin, obstacle-budget preservation, and the exact reported Mist start. All existing shared navigation tests remain required. Goblet arrival is confirmed by this export; smoothness and Mist completion after this update still require live confirmation.
