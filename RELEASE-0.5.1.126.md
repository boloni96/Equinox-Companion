# Companion 0.5.1.126 — Where is it cheapest?

Adds `/whereischeap <item name or ID>` as a separate window, with an inventory context-menu entry and `/equinox cheap` fallback. Partial item names show a selection list.

- Universalis searches across North America, Europe, Japan and Oceania by default, plus individual region and HQ/NQ filters.
- Cheapest unit price first; quantity, stack total before tax, world, DC, region, per-listing review age and copy-location action.
- Up to 100 cheapest listings per region, with a clear recorded-data notice and partial-region failure reporting.
- Background requests, cancellation when changing search, 60-second cache, and bounded result rendering. Local item names are indexed in small batches on first opening.
- Native collapse/Esc minimizes to its own gold coin/search icon; no new Companion tab.

Validation: local Release build succeeds with no warnings/errors. Existing regression suite and new market parsing, sorting, cache, cancellation and partial-outage tests pass. Universalis Europe response checked directly. In-game visual/input/FPS verification remains for the user.
