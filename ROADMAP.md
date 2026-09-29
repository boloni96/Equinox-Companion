# Equinox gardening and house roadmap

## Shipped in 0.2.0.0
- Opt-in automatic local tending history, independent of the five-minute diagnostic recorder.
- A Tend Crop selection followed by log 4017 within three seconds, with matching character, house and target, confirms tending. A healthy-crop log or menu choice alone is insufficient. Duplicate responses retain one event ID.
- English menu titles identify physical patch and bed. Records retain character, house, patch, bed, selection time and confirmation time. Latest 10,000 tending records are retained locally.
- Character house visits show the latest detected entry ("touched"). Startup inside is an observation with unknown entry time. Missing history is unknown; visits do not imply ownership or a verified demolition reset. Latest 500 house observations are retained.
- No website connection yet; no planting, harvest or fertilizer success records promoted from diagnostics.

## Planning only — do not implement yet
Requested 2026-09-29:
- In-game selection: final crop → recipe step → house, patch and batch; favourite recipes first.
- Show all beds, required seeds and soil, planting order, and existing plants to keep.
- User performs game actions. Detected actions update the website with actual crop names, planting times, tending and harvest records.
- Growth timers, estimated harvest times and tending reminders belong in the plugin.
- Keep per-bed crop-cycle records. Two batches in the same physical patch must stay independent; patch number is not batch identity.
- Planned actions and confirmed actions are separate. Selecting a plan never starts a timer. Record what actually happens and flag differences from the plan.
- Website focuses on all gardens, batches, progress, upcoming tasks and manual corrections.
- Character house details include whether an inside entry has been recorded, and when. Match to the website's character/property roster when integration becomes available.

## Integration gate
User reports the newest website package is V7.9.13. This is not yet independently verified as the deployed version. Public fetch was blocked on 2026-09-29. Confirm the live version and use that deployment's source before integration. The website is deployed manually to Cloudflare; the older Journal GitHub repository is not an authoritative deployment source.

Preserve the earlier error-1102 fixes: metadata-only storage inventory, bounded batches and streamed journal reads. Use small indexed, deduplicated game events; avoid rewriting/loading the full journal per action. Verify actual Cloudflare CPU/request behavior before declaring the free tier integration ready. Local tracking in this release makes zero website requests.
