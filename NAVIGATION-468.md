# Navigation 0.5.1.468

The .467 live report confirmed arrival at Goblet W18 P22 without recovery. Planning still took about 77 seconds, followed by 20 seconds walking. Mist W24 P42 timed out after 90 seconds; its reverse search expanded only the destination root. Two Limsa aetheryte approaches also timed out.

Changes:
- Plot/door candidate selection now requires an incoming swept connection on the route search lattice, rather than accepting standing room alone. This rejects locally disconnected candidate floors; it does not prove global reachability.
- Aetheryte approach ground selection applies the same connection check (except when already standing there). If the nearest radial approach fails, check alternate sides at the same horizontal radius, with visibility validation. Selection uses a temporary planning collision cache.
- Route expansion tries two-unit as well as unit connectors. Every connector is swept with existing support, obstacle and height checks; connectors above the 2.9-yalm traversal limit are excluded. Unit connectors remain available at every node, with no coarse-only restart.
- Plot approach selection uses a temporary collision cache to avoid duplicate geometry reads during candidate selection.

Validation: independent navigation regressions and Release build pass. New tests cover disconnected standing points, connected floor, rejected isolated aetheryte approaches, 48-yalm open road (24 expanded points), and short-step fallback when longer edges are rejected. Existing stacked-floor, one-way, obstacle, resumable-budget and actual Goblet wall-material tests pass.

Live performance and Mist/Limsa completion remain unverified. Longer connectors reduce expansion in the open-road fixture; this is not a claim that the 77-second live planning delay is solved.
