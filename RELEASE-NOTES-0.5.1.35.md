Equinox Companion 0.5.1.35

Successful garden harvesting now clears the exact bed immediately and sends the existing garden.empty event to the shared journal. Previously the crop stayed ready until its empty numbered menu was opened again.

Confirmation requires a numbered Harvest Crop selection, the actual crop identified by that bed's recent game inspection, and the matching received-crop item response. Item parameter positions come from the installed game's LogMessage templates. A plan, an opened menu, unrelated loot or crossbred seed reward alone cannot clear a bed. Failure responses cancel the pending harvest; stale, other-character, other-house and other-bed responses are rejected. Login/zone resets discard pending selections. Existing empty-menu detection remains available when confirmation cannot be matched.

The update uses the existing empty-bed protocol, so Journal V7.11.46 supports harvest syncing. Optional Journal V7.11.47 raises the centre Plan/Start caption to match the adjoining bed captions on desktop and mobile.

Validation: Release build succeeds with zero warnings/errors; 538 C# checks pass, including receipt mapping, inspection/selection correlation, failure and identity boundaries, all-eight-bed clearing, effect/timer removal, completed-plan preservation and stale-ready rejection. Existing website live-roster garden sync checks pass. The live game harvest sequence still needs in-game verification.
