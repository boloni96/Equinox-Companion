# 0.5.1.48 — Requested rollback

Source/tests restored to published 0.5.1.46, version bump only.
728 checks passed; Release build zero warnings/errors. Live harvest and
performance confirmation pending. No investigation fixes included.

# 0.5.1.47 — Garden update efficiency

802 checks passed. .NET 10 / Dalamud API 15 Release build: zero warnings/errors.
6,000 actions / 30 batches: one new tend causes one projection; 3,000 unchanged
batch draws cause no extra projections. Every batch matches full-history replay.
No live FFXIV performance measurement.

# 0.5.0.2 — recovered checkpoint and background housing

Release build: zero warnings/errors using Dalamud API 15 references. Existing synthetic gate suite passes. Installer package matches the compiled DLL, manifest, artwork and catalogue. Journal V7.10.2 tests cover two-client background housing, tenant/FC membership eligibility, ownership changes, replay/stale events, no R2 access, ETags, bounded cache, full 50-event upload query count, and durable queue preservation. Native FFXIV and production Cloudflare checks remain outstanding. The user explicitly authorized publication of this prepared build on 2 October 2026.

# 0.4.1.6 — exclude shared private houses from character timers

Validated owner/tenant/unknown/ambiguous name/home-world cases and FC membership projection. Local owned-estate details require ownership evidence. Shared private details remain expanded-only. Release build and gate suite passed; native appearance needs an in-game check.

# 0.4.1.5 — consistent house order

Explicit Private-before-FC display ordering replaces alphabetical or source ordering. Both local and shared tooltips follow their corresponding detail-list order. Release build passed with zero warnings and errors.

# 0.4.1.4 — persistent error diagnostics

Release build: zero warnings/errors. Gate suite verifies persistent error history, repeated-event suppression, rotation and nonfatal filesystem failures alongside existing sync/housing/garden checks. Error log uses fixed application descriptions and exception type names only. Diagnostic exports now report the actual plugin version and include up to 2,000 recent log lines. Native Diagnostics controls require an in-game check.

# 0.4.1.3 — compact coloured hover dates

Connection also shows the saved pairing key masked by default, with Show/Hide and Copy controls. The UI explains this is read/write game-sync access, not view-only guest access. Closing the window remasks it.

Settings now has General, Characters and Connection subtabs below the main tab row.

Removed duplicate tooltip status rows. Each estate date line uses its own timer colour, including paused/unknown shared entries. Removed the numeric timezone offset while retaining local-time conversion. Release build passed with zero warnings and errors; native appearance requires an in-game check.

# 0.4.1.2 — character list controls

Last eligible entry dates in character-bar tooltips, assembly version label, server/data center/region on bars, houses-first sorting, persistent per-list custom ordering, hide and restore in Settings. Hidden IDs and custom order are saved separately from observation records and shared roster data. No changes to event uploads or website data. Logged-in pinning is a stable view-only sort, skipped by Settings; local matching uses content ID and shared matching requires a unique name/home-world match. Hidden characters remain hidden.

Validation: Release build passed with zero warnings and errors. Reviewed ID-based ordering/filtering and balanced ImGui scopes. Native tooltip appearance, ordering controls and restore interaction require in-game verification.

# 0.4.1.1 — collapsed characters

Both local and shared character headers now default to collapsed. Manual expansion still works; split house-status colours stay visible. Release build passed with zero warnings and errors. Native appearance requires in-game verification.

# 0.4.1.0 — shared profiles, split house status, settings and local chat

Requires Journal V7.9.23 for shared-profile downloads. Keep the existing pairing key on both installations. Includes the incomplete-character queue fix from 0.4.0.4 and supersedes the unpublished 0.4.0.5 colour-only package.

Private/FC header halves have independent status colours. Tests, Characters & housing, Settings and per-profile tabs can be dragged into a different order. Shared profile data is read-only and cached separately from local game observations; it is never uploaded as newly observed game data.

Settings adds opt-in local-only entry messages and opt-in shared refresh while closed. Tracking and queued game uploads do not depend on window visibility. The shared roster checks at most once per minute automatically and uses ETags. Closing the window pauses only shared-list refresh unless background refresh is enabled. Revoked keys clear the shared cache on the next check; changing pairing keys clears it immediately.

The server shares only roster and house-summary fields. The website must apply incoming game events and save to publish an updated summary; plugins do not download pictures, notes, full journal history or credentials. FC Activity inference and online-status timer resets are not implemented.

Validation: zero-warning Release build; existing gate suite and private queue replay; private/FC/guest/login/workshop notification rules; website storage/event regressions; same-key two-client roster reads, three-profile projection, role eligibility, privacy allowlist, revoked-key/unauthenticated/full-journal access rejection, ETags and no R2 access during roster GET; browser save-to-roster integration. Actual native tab appearance, drag behaviour and local chat require in-game testing. No private diagnostics or screenshots in the public release.
