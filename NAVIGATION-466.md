# Navigation 0.5.1.466

The 0.5.1.465 live report found an 86-point route in about 69 seconds (997 expanded points), walked up the first stairs, then stalled near (155.64343, -25.999994, -19.400673). Backtracking completed; subsequent recovery timed out after 15 seconds.

The obstruction probes identify material 0x2004 at z=-19.931623, about 0.53 yalms ahead. All default 0x4000 support-filter probes missed it; broader probes detected it at foot, body and head heights. This is concrete evidence that support-only queries were falsely approving this movement segment.

Changes:
- Body sweep rays, wall-clearance preference, headroom and interaction visibility use the union of support 0x4000 and observed barrier 0x2000 categories (mask 0x6000, any-match).
- Downward ground-support queries retain their 0x4000 filter. Unrelated 0x1000 geometry is not added.
- Raw-ray cache keys include the material mask so results from the two filters cannot mix.
- Obstruction diagnostics retain default and broader probes and add the actual movement-filter result for comparison.

Tests reproduce the reported wall plane/material: the old filter permits the path; the movement filter rejects it; walking away on a clear road remains possible. Navigation regressions and Release build pass.

Live completion is not yet verified. The 69-second planning time remains unresolved; this release addresses the proven missed wall, not a claimed planning-speed fix.
