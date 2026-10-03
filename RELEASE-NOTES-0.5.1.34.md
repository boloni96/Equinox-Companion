Equinox Companion 0.5.1.34 / Journal V7.11.46

Plant -> Tend -> Cared for -> 12 hours -> Tend -> Cared for
- A planting timestamp starts crop growth and neglect estimates. It is no longer treated as proof that a Tend action happened.
- New permanent crops show tending droplets immediately. A later recorded tending clears them, wets the soil and starts the 12-hour reminder interval. Growth timestamps do not restart when tending.
- All eight beds in an 8-step plan follow this cycle, including Bed 1.
- In a 1+9 plan, only the temporary step 1 starter skips tending prompts while waiting for its replacement. It has the starter badge; after the neighbours are confirmed it has the replant badge/instructions.
- After the final step 9 planting, Bed 1 follows normal care and needs its first tending. The exception is based on the active temporary-starter state, never merely the bed number.
- Old records whose watering timestamp equals their planting timestamp also request the first tending. A later tending timestamp keeps its existing 12-hour interval.

Native seed picker
- Picker entries use the native list's logical index and the planting agent's inventory slot to distinguish seeds sharing the same bag icon.
- Validate menu/list counts, renderer index, inventory item ID and displayed icon before using the entry binding. Exact native item tooltips remain supported.
- Existing green/red selected-item outlines stay in place. No game actions are performed.
- The native picker requires an in-game check with two different seeds sharing an icon, duplicate inventory stacks and the actual required seed. Compilation and logic checks cannot verify native rendering here.

Validation
- 496 C# checks; Release build against Dalamud API 15.
- Browser checks for temporary starter before/after the neighbours, permanent 8-step Bed 1, final replant -> first tending, wet soil, independent beds, animation/reduced motion and existing shared controls.
- Website state/roster/live tests and all 108 crop catalogue artwork references pass.

Website: upload Equinox-Journal-V7.11.46-Cloudflare.zip directly to the existing Pages project. No website deployment is performed by this release.
