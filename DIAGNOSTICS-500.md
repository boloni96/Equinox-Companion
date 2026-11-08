# 0.5.1.500 — Retain routine reasons and expose blocking prompt text

Add Settings > Activity reasons with a bounded session history of routine status changes and terminal details. Capture runs independently of the activity icon and window visibility every 250 ms. Include the same history, capacity and eviction count in diagnostic exports. Cover the routine families exposed by the work indicator, including world/housing/shopping travel, navigation, gardening, quests, character switching, storage, transfers, trading, market prices, rewards, following and delivery.

Show explicit missing-reason text when a routine supplies no status or ends with its unchanged active status. This is an observability gap, not an inferred cause. Avoid idle startup noise and repeated identical waits. Retain up to 200 changes; very short start/stop transitions between samples may be missed. This change does not claim every internal early-return branch now has a specific reason.

Travel statuses now quote unknown SelectOk messages and World Visit confirmations, plus confirmations blocking party leave. Character-selection server locks receive a concrete waiting status. User-paused market pricing explicitly says it was paused by the user. No extra game dialogs are auto-confirmed.

Validation: Release build and gate suite; tests verify terminal cause retention, missing causes, silent ends, unchanged-wait deduplication and history bounds. Native UI presentation remains to be checked in game.
