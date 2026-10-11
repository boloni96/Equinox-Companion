# Companion 0.5.1.127 — Market refresh and travel

- `/whereischeap` automatically checks Universalis while its window is visible. Choose 15, 30 or 60 seconds; default 30, saved across sessions. Closed/minimized windows start no new requests; overdue searches refresh on reopening.
- Keep the current results visible during checks. Update changed prices and remove listings when successful responses no longer contain them. Failed regions keep clearly flagged older listings; repeated failures back off to five minutes. Listing review age remains separate from the latest check time.
- Refresh now and timed checks bypass the initial-search cache. No overlapping requests.
- Each result has a Travel button using Lifestream's existing ChangeWorldById IPC. It reports accepted/declined/unavailable requests, avoids replacing a busy trip, and marks the current world Here. Region access remains subject to Lifestream and the game; DC travel can log the character out and in. No automatic buying or market-board navigation.
- Header shortcut immediately left of Do I already have it, matching the requested position.

Validation: Release build and regression tests including refresh timing, partial-outage preservation, sold-listing removal and cache bypass. In-game appearance and travel still require user verification.
