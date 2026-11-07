# Residential shard elevation 0.5.1.476

Includes the shared-planner changes and regression coverage in NAVIGATION-475.md.

The 022044 export shows housing stage 7 repeatedly reporting "No supported
aetheryte approach": player (17.585922, 5.9999895, 130.11188), shard
(-4.5625, 9.994568, 96.94055). The approach inherited player Y=6, so bounded
support probes never reached the shard's platform near Y=10. The radial approach
also collapsed to the shard center because the starting height difference was
larger than the interaction radius. This is a destination-height selection bug
before path search, separate from stage-10 planning timeouts.

When the height difference exceeds the interaction stand-off radius, use the
shard elevation as a destination-ground hint and preserve radial stand-off.
Ground checks then prefer that destination height and bounded downward probes,
rather than accepting a floor solely because it matches the starting player.
Alternative radial approaches preserve the same height hint. Native support,
incoming-edge validation, route collision checks and actual interaction-range
verification remain required; the hint is not permission to walk across floors.

The new test uses the reported coordinates with a synthetic platform: the old
helper fails, the new helper finds the destination floor. It also checks a lower
overlapping floor, a descent, stand-off distance and missing geometry. Existing
floating-origin / raised-platform approach regressions and the complete navigation
suite pass. Live travel still requires verification; this does not claim that all
housing routing or generic entrance detection is complete.
