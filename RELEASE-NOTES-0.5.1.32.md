Equinox Journal V7.11.44 / Companion 0.5.1.32

Crop artwork correction
- Unplanted plans use the full-size -live.png identity illustration, with the plan veil and plan tooltip. Seedlings are no longer shown before planting.
- After planting, recorded growth below 33% uses -seedling.png; 33–99% uses -growing.png. Growth remains an estimate from the recorded planting/harvest clocks.
- During the first 12h after recorded care, use -seedling-wet.png or -growing-wet.png with wet soil. These sprites already contain droplets, so do not add the separate droplet layer again.
- If an actual crop has no planting/growth clock, keep growing art without inventing a growth percentage; wet care uses growing-wet. If there is no crop artwork, keep the standalone droplets so recorded watering is still visible.
- Mature uses -mature.png and sparkles, including Estimated ready at 100%; game-confirmed readiness remains a separate flag. Wilted/dead artwork takes priority over healthy growth estimates.
- The older full-size -wet.png is intentionally unused. It is a wet copy of the live identity illustration; plans have no care state. It remains in the complete inventory and preview gallery.
- Known actual crops always override planned-crop identity, even when the plan differs.
- Updated the full inventory with these exact selections. Flowerpot-only art remains catalogued, without claiming a supported outdoor use.

Installation
- Companion is published through the existing GitHub installer feed. Update to 0.5.1.32.
- Extract Equinox-Journal-V7.11.44.zip, then upload its inner Equinox-Journal-V7.11.44-Cloudflare.zip to the existing Pages project. No website deployment has been performed by the assistant.
- This is a focused artwork correction on top of V7.11.43 / 0.5.1.31. House links, owner/FC reminders, icon placement, double-click cancellation and status sync are preserved.
- Native in-game appearance remains to be checked by the user.

Effects and colour preview
- Water droplets, ready sparkles and wilt mist alternate one second on / one second off. The crop, soil, border and information targets stay present. Wet sprites alternate with the matching dry sprite; no second droplet layer is added.
- The website offers Animate water, sparkles and wilt mist, shared with Companion. Turning it off keeps effects steady. Website reduced-motion preferences are respected.
- Both artwork test views include an Oldrose Possible flower colours demo: white, black, rainbow, one second each, with pause and a normal-colour caveat. Equal display durations do not imply equal chances.
- Flower colours are a preview only. Automatic indoor-pot detection/sync is still pending. The research note records verified game wording and the test evidence needed before enabling it.

Validation: 450 C# checks passed in this run. Browser checks verify one-second effect phases, persistent plant visibility, shared settings, reduced motion, the paused/non-mutating flower preview, and existing hover/layout behavior. The plugin compiles for Dalamud API 15; native game appearance still needs a user check.
