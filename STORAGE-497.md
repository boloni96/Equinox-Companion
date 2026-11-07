# 0.5.1.497 — Search dresser source categories before skipping

The .496 report ended with 0 stored and 214 skipped. Its retained last 100 events contained 34 missing-list skips and 25 generic eligibility skips; the report cannot identify the precise filter state or cause of every missing row.

Individual storage now searches the current native list, then uses the registered next-category button event to visit the other source categories. It waits for the selected category to change, a 750 ms settling interval, and completed native population before reading candidate rows. Each search is bounded by the six category slots or a completed cycle; a category that does not change stops with an explicit error. Exact container/slot/item matching remains mandatory. An item absent throughout the search is kept and reported with advice to check game list filters. Existing game filters are not cleared or protection settings changed.

Eligibility diagnostics now distinguish gear-set protection, dyed gear (including channel two), attached materia, repair condition, equipped gear, duplicate copies, Keep reservations, quality/tradability/category filters and missing source data. Storage history records category transitions and preserves the item ID on missing-list skips.

Reference for native category buttons and event dispatch: SpringCleaning DresserNativeControls.EnsureDresserCategory and NativeUi.DispatchButtonClick at https://github.com/OhGodAGirl/FFXIV/tree/95f5b9bd9ed5812f58c3c4f6e31bd68e7f504eaa/SpringCleaning/src/Features/Migration/Interop .

Validation: Release build and gate suite, including an exhaustive combination check that diagnostic reasons preserve the prior copy eligibility predicate. Live category changes and successful transfers require in-game verification.
