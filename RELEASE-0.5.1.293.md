# 0.5.1.293

- Assignments keeps a stable popup size with a scrolling list, fixing progressive shrinking on every frame.
- Chest options keeps a stable width when using crystal filters.
- Organizer temporarily pauses rendering its contents while its outer window is resized; active transfers continue.
- Long status/caption text uses bounded binary-search measurement rather than removing one character per measurement.
- Inventory lists use explicit row heights during clipping.

Validation: regression suites and plugin build run in the release workflow. The reported full game freeze requires in-game confirmation; these changes remove identified layout feedback and unbounded text measurement paths.
