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
