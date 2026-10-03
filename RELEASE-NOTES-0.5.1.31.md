Equinox Journal V7.11.43 / Companion 0.5.1.31

Garden reminders and sync control
- Moogle-and-house shortcut is 25% larger than Sync and shares the Batch tabs’ top edge, immediately left of Sync when present. It opens the exact house and selected batch on the website Gardening section, and remains after Sync hides. Deep links retry after shared data loads.
- Sync mouse hover closes when the pointer leaves; keyboard navigation retains a focused tooltip. The short hover covers start, Beds 1–8, restart and website save. Full instructions remain in help.
- Chat care reminders identify Owner: <character> or FC: <company>, world, district, ward, plot and batch. Status colors and per-login reminder suppression remain. There is no /equinox-for-details replacement for the address.
- Batches are grouped per house/status and chat lines are bounded; reminders are not sent for each bed. Unknown owner data is labelled, never replaced by the viewer or gardener.
- The visitor bed-sync icon is aligned at the right of the Batch tab row. It hides once all eight beds have a game observation, including confirmed empty or an observation whose crop identity remains unavailable. A saved plan or manual planting date alone does not count as inspection.
- Double-click Sync cancels its unfinished walk and clears the number; the icon remains idle. The double-click release cannot accidentally restart it. Single-click starts again at Bed 1; a single click while running restarts.
- Pending upload is separate from inspection completion. Recalibrate beds 1–8 remains under 'Waiting for a garden or house to connect?' after moving/replacing a patch. An active incomplete guided walk keeps its control visible.

Design review improvements
- New placement addendum reviewed across all 1,591 image entries. Top-right now uses only plan/matched/different or no-plan meaning; lower-right retains actual care even when the plan differs. Queued is a secondary icon beside care. Framed care overlays remain documented alternatives, not incorrectly positioned controls.
- Plan outlines use the board gap outside the care perimeter; selection is inset. Matches-plan no longer adds a harvest-ready border.
- Known actual crop artwork and caption remain visible during an unfinished or different planting plan. The original top-right target shows plan/planting information; the lower-right target shows actual crop/care.
- The 81 outdoor seedling sprites show the tiny planned-crop preview and estimated growth below 33%. At 33–65% use growing art; at 66–99% retain growing art with late-growth text; at 100% show mature art and sparkles with Estimated ready / check in game. This does not set the confirmed Ready flag. Missing planting/growth timing uses growing art. Known actual crops override plan previews; confirmed wilt/death overrides healthy timing art.
- Secondary queued icon and detail text identify unacknowledged local plugin actions or actual website bed records different from the last acknowledged save. Server acknowledgement is not proof another client has refreshed. A later local edit stays queued when an older save finishes.
- Fishmeal illustration appears beside an actual fertilizer record. Fertilizing does not reset tending. Plugin fertilizer events honor the garden tracking setting.
- Keep-mature metadata and care icon now agree in the plugin. Website garden tips are accessible by keyboard and touch.
- Tended-by name and home world come from the actual care event at the relevant timestamp. Old history cannot attach a previous plant's gardener to a replant or a manual update. Newer care wins over delayed older events and remains specific to one bed.
- Storage/paused remains deferred: no accepted storage lifecycle exists. Offline is not storage. Confirmed wilting now uses wilted crop art, mist, border and lower care icon; it remains distinct from an estimated wilt/death timer.
- Alternate wet/legacy images, flowerpot colors and reference atlases are still listed honestly in the complete inventory. Gallery display is not counted as live use.

Garden message research outside the five-minute test
- While garden tracking is enabled, recent garden-associated system messages, menu copies and garden native-log parameters are retained in a bounded local history. Export diagnostics includes up to 400 recent records, even when no five-minute recording was started.
- History survives restarts and rotates around 256 KB per file with one previous file. It is not sent in pairing traffic. Player chat is not subscribed to for this history.
- The saved reports inspected for this update establish empty-bed text and named mature-crop text; ordinary saved records also contain confirmed tending. They contain no preserved growing-stage wording. Log 4017 is already classified as a doing-well status, but its text alone is not proof of tending or a named growth stage.
- Crop names are checked against the game's item data, not the artwork catalogue. A new game's crop name can remain visible before a future artwork/timing update. Existing growing-crop identification still needs a verified message/target test; no identity is guessed from the plan.
- Verified English inspection strings come from xivapi/ffxiv-datamining commit d9582a624eaec69d45649366dee801c2f3a6e9b3, csv/en/custom/001/CmnDefHousingGardeningPlant_00151.csv, rows 7–10. Guide paraphrases are not used as exact parser strings. The text distinguishes health and harvest readiness, not multiple healthy size stages.
- New growing/wilting/dead observations require a game item name, fresh garden target and matching numbered menu or validated visitor calibration. They preserve watering and gardener attribution. A different observed crop clears the previous crop’s clocks. Fertilizing cannot cure wilt. Delayed status cannot override newer tending, planting or readiness. A later healthy inspection supersedes an already elapsed death estimate without inventing watering. New status sync waits for website protocol 11.
- GARDEN-MESSAGE-TESTS.txt describes the next checks. They can be collected during ordinary tracking, then exported without starting a timed test.

Validation and installation
- Companion compiled for Dalamud API 15. 462 C# checks passed, including actual uploaded empty/mature logs, observation completeness, reminder identity, chronological care, local history persistence/rotation and file-error recovery.
- Website checks cover actual crop/seedling rendering, separate plan/care hover and touch/focus, centered desktop/mobile gardens, icon-caption spacing, Fishmeal, queued-save races, actor chronology, per-bed timers, visitor mapping, authentication and event validation.
- The build retains the existing unavailable NuGet vulnerability-feed warning. Native in-game icon/tooltip appearance and growing/wilt/dead capture still need a user test; those strings are source-verified, while empty/mature messages were replayed from your supplied logs.
- Upload Equinox-Journal-V7.11.43-Cloudflare.zip to the existing Cloudflare Pages project with the current database/bindings. The assistant has not deployed the website.
- Source ZIP includes source, tests, full inventory with placement guidance, original placement addendum, optional offline page and these notes. Companion is published separately through the existing GitHub installer feed.
