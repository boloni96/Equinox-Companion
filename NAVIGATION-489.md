# Aetheryte interaction regions 0.5.1.489

The Limsa shopping-travel screenshot showed “Waiting for walkable ground beside the aetheryte”. That status comes from a pre-navigation ring-sampling gate: failure to select one supported radial destination prevents the navigator from starting. The screenshot alone does not establish which native ground probe failed.

World Visit, aethernet and housing target approaches now request the shared navigator's interaction-region search directly from the player's supported position. They no longer require a selected radial floor destination before starting. The target's position guides the search, but a reachable supported position within the target-specific distance (with margin) and clear interaction visibility is enough to terminate it. Main crystals can therefore have raised or obstructed origins without requiring those origins to be walkable. Map guidance remains enabled for travel; garden-local geometry mode remains unchanged.

Normal live interaction range checks still stop walking and the existing callers still verify the actual menu. No collision checks or interaction distances were relaxed. The general navigator's finite planning, stall and cancellation rules remain.

Validation: plugin build, navigation suite with floating crystal and alternate visible street-side goal-region cases, and gate regression suite. Actual Limsa marketboard-to-main-aetheryte behavior is not yet verified in game. No Limsa-specific route coordinates were added.
