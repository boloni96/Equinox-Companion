# Performance corrections — 0.5.1.501

The 31 reports from .468–.500 show recurring elapsed-time spikes in window drawing, inventory refresh, garden projection and navigation. Other-plugin stalls in Dalamud's log also coincide with whole-game freezes; these fixes do not claim to eliminate them.

Changes:
- Fashion shortcut completion/tooltip queries use the incremental fashion observation index rather than scanning all discoveries on every draw.
- Organizer stock refresh reads an indexed latest storage observation per actor/container (latest timestamp, first entry on ties), and totals current player bags/crystals in one pass. Transfer eligibility and receipt checks still use live inventory.
- Gardening projection/mapping caches depend on the garden observation revision; unrelated collection or inventory observations no longer trigger a rebuild. Garden actions and roster changes still invalidate immediately. Garden history reads use the garden-only index, preserving original event order.
- Reports retain the 32 largest distinct-frame stage samples within ten minutes separately from the chronological ring. Whole-game intervals also retain timestamps, without claiming plugin attribution. Reset clears both histories.

Validation: Release build and full GateTests pass, including a 90,000-unrelated-event index test, no-rescan checks, garden invalidation, storage owner/date/tie/replacement checks, and major-hitch survival after chronological eviction, expiry and reset. Ten existing compiler warnings remain. In-game FPS improvements require a new report; startup indexing, remaining garden work, navigation, save serialization and other plugins can still stall.
