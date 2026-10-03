Equinox Journal V7.11.40 / Companion 0.5.1.27

Separate garden icon tooltips
- Top-right: planned crop, soil, planting order, starter/replant instructions, confirmation and mismatch information. Beds without a plan explain how to create one.
- Lower-right: actual crop and soil, latest care and gardener, fertilizer, observation time, harvest readiness and estimated risks. Hover is limited to the existing icon above the caption. Website supports keyboard focus and tap, with Escape/outside dismissal.
- Tooltip headline and guidance use the same icon selection as the artwork. Preserve original icon positions, all borders and bed captions.
- Keep native Dalamud title-bar collapse-to-floating-icon behavior from 0.5.1.26.

Lower-right icon artwork reviewed against the original Garden Design pack:
water.png / tend.png: blue drop; recently tended versus tending suggested.
ready.png: green sparkle; confirmed harvest readiness. Consider whether the plan still needs this neighbour.
growing.png / keep-mature.png: sprout; growing versus explicitly keeping a mature neighbour.
empty.png: outlined empty square; current empty bed, ready for a planting plan.
unknown.png / check-maturity.png: question mark; missing observation versus an elapsed growth estimate requiring an in-game check.
warning.png: yellow triangle; planting mismatch/wrong-bed warning, with full instructions in the top plan tooltip.
at-risk.png: red triangle; estimated risk, inspect and tend if alive.
wilting.png: drooping sprout; estimated wilting, inspect in game.
dead-estimated.png: crossed mark; estimate only, verify before removal.
Tooltip metadata: actor.png is the recorded gardener, clock.png is timing, fertilized.png is the fertilizer bag, sync.png is the observation timestamp.
Other unused icon states are not claimed as detected merely because their images exist.

Validation: plugin builds; desktop/mobile layout, separate hover/focus/tap targets, icon/caption alignment and existing bed actions pass. Current user backup checked locally: 112 beds across 12 linked gardens. Artwork/state tests and hosted/offline parity pass. Native FFXIV visual verification remains for the user.
