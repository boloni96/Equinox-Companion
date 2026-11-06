# Navigation 0.5.1.467

The 0.5.1.466 report contains city-aetheryte approach failures before reaching the Goblet route. Several short approaches immediately reported arrival with no movement, then remained at housing stage 2. A manually opened residential menu was recorded but not consumed because stage 2 still required the approach predicate. A separate nearby approach timed out after five seconds. The corner fix is not verified by this report.

Changes:
- Restore the pre-.466 support-filter visibility ray for interaction markers. A target marker can be inside its own movement collider; treating that collider as an intervening wall can reject a valid interaction. Actual body movement, headroom and wall-clearance queries retain the broader .466 movement mask.
- At housing stage 2, recognize an already-open SelectString menu only when its options match the existing localized residential/ward travel text, then hand it to the existing menu stage.
- When local walking reaches its goal but interaction reach is still unverified, retry closer using the existing bounded target-specific attempts; stop explicitly after those attempts rather than repeatedly returning an idle arrival.

Release build, transition tests and navigation regression tests pass. The visibility self-obstruction explanation is a code-supported hypothesis, not a measured ray hit in this export. Live city interaction, the separate nearby-approach timeout and final Goblet route completion require verification. No general planning-speed improvement is claimed.
