# Companion 0.5.1.128

- Where Is Cheap displays the item name after right-click selection, retaining the exact item ID internally.
- Region and World filters: selected-world searches query Universalis directly, with separate cache entries and the existing 15/30/60-second visible-window refresh. Listing identity supports the single-world API response.
- Context menu entries have an explicit prefix, eliminating the repeated missing-prefix warning.
- Collection catalogue file parsing runs in the background. Quest and hairstyle sheet enumeration stays on the game thread and is spread across frames with a 1 ms loop budget (individual sheet reads cannot be preempted).
- Details, collections, storage, voyages and Fashion observations are staggered instead of starting in the same frame, retaining a five-second per-stage cadence.
- Unchanged storage containers use structural comparisons instead of serializing both snapshots. Quantity, quality, slot, empty-container and periodic refresh changes still record normally.
- Header house shortcuts reuse their display lookup; roster changes invalidate it immediately and character data is checked at least once a second.
- Performance diagnostics show stage peaks for discovery, catalogue, sync queue, roster/maintenance, submarine cache, windows and overlays.

Validation: Release build, zero warnings/errors; 1,621 regression checks passed; live Universalis world-response format verified. In-game timings require a new session on the user's computer. This is a performance update, not a claim to fix the separately investigated travel crash.
