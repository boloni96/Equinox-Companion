# Companion 0.5.1.51

Gardening guide batch selection now follows temporary (soft) targets as well as normal targets. A missing mapping is retried at most four times per second, allowing a mapping that arrives after selection to open the correct batch. A resolved selection is consumed once, so manual tabs remain usable until the target changes. Partial bed calibration still prevents automatic switching.

This is a guide-navigation change. Game observation snapshots, harvest/empty-bed handling, garden timers and planting actions are unchanged. Unknown, moved, ambiguous and different-estate targets remain rejected.

Release build: zero warnings/errors. Existing 820 checks plus five mapping-arrival/manual-tab regression checks passed. Native game selection and live performance still need verification.

Quick check: update to 0.5.1.51, open /gardening at a mapped multi-batch estate, select a bed in Batch 2 while Batch 1 is displayed, then Batch 1 again. Repeat with the garden interaction menu open. Manually select another tab without changing the game target: it should remain selectable. Try an unmapped target: no guessed switch.
