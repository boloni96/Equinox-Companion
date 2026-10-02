# Companion 0.5.1.1 / Journal V7.11.1

This is an in-game validation release. Build and automated checks pass; native game observations still require live verification.

Deploy Journal V7.11.1 to the existing Cloudflare Pages project, open and save it once, then refresh shared profiles in Companion. Keep both users on the existing pairing key. Storage events are held locally until the website advertises protocol 3; existing supported events continue syncing.

- Restored the missing hairstyle catalogue and added release checks so hairstyles cannot silently disappear again.
- Remember loaded personal inventory, equipped gear, Armoury Chest, saddlebags, Armoire, Glamour Dresser and individual retainers. Unopened containers are not cleared. Retainer and FC Chest observations require a loaded, identified context.
- Locations include inventory page numbers, retainer names and retainer inventory numbers. Shared FC Chest pages are displayed separately from personal ownership.
- Dedicated Submarines tab uses the newest observation per FC. Header status sits beside the approved icon.
- Same approved icon normalized to 512×512; compiled manifest and feed include IconUrl. Original artwork remains in assets/companion-icon-original.png.
- Incomplete login records are logged once per session, retained for diagnostics and never allowed to block valid actions.
- Current storage snapshots survive the 60-day acknowledged-history pruning. Storage writes are batched to avoid repeatedly writing the full config during one observation pass.

The website adds equipment/clothing, one Obtained filter with Not learned markers, compact job rows, game-observed Fashion completion, automatic exact Lodestone matching, event history by year, all-account catalogue coverage, per-photo guest sharing, a moderated Guest Welcome Book, and protected photo cleanup.

Live checks: update both clients; open each storage location and retainer; move an item between inventory and retainer then use/unlock it; verify per-character isolation after switching characters; read private/FC placards and Company Profile; open a numbered garden bed without harvest permission; speak to Masked Rose after judging; open workshop voyages; verify paired green entry notices/red 30-day reminders. Housing times remain recorded-entry estimates; game demolition status is authoritative.

Equipment sources are not yet fully classified. Unknown/ambiguous quest and reward mappings remain unconfirmed. Reused quest bits cannot prove which past rerun was completed. Unlinked characters still require a person/account assignment. Full journal and garden application requires the website open; compact known-estate and voyage updates can reach paired plugins in the background.

Blue branding patch: sapphire Companion icon with silver highlights; Journal uses the matching blue book icon.
