Equinox Companion 0.5.1.125 — Submarines render work

Follow-up to continued FPS loss with the Submarines tab open on124.
- Remove the250ms expiry from submarine history projection. Rebuild when the
  roster or discovery count/latest ID changes; no idle history rescans.
- Replace nested history/character scans with one history pass and indexed
  identity matching. Preserve latest source times, FC membership and ambiguity guards.
- Reuse warning summaries within each second, invalidating immediately on source
  or reserve changes. Prepare character ordering only for the visible person tab.
- Add Submarines draw/peak and last index rebuild time/count to tracking diagnostics.
  These measure CPU work, not total game frame time or GPU performance.
- Retain124 workshop-aware warnings and all observation/upload schedules.
  Gardening retains its existing250ms display cache and per-frame artwork behavior.

Validated local Release build and focused history/caching/identity regression tests.
Actual game FPS improvement remains to be checked after updating. If slowdown
persists, capture the Submarines timing values in tracking diagnostics and compare
the same tab with all account/character sections collapsed versus expanded.
