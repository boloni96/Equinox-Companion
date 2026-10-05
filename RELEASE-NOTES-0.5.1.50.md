Equinox Companion 0.5.1.50 — Gardening and QuickLoot feedback
Journal stays V7.11.66; no website deployment or re-pairing required.

- Characters & housing is now My Empire, including character-order settings.
  Existing tab order, data and launcher/keybind settings are retained.
- /gardening is the primary command; /planting and /equinox planting remain
  aliases. /equinox gardening also works. Feature help/launchers use Gardening.
- The open guide follows a newly selected, previously mapped bed to its assigned
  batch. Full estate/world identity, newest calibration, coordinates and physical
  patch must match. Unknown, moved, expired or ambiguous mappings do not switch.
  Partial guided calibration stays on its chosen batch; manual tabs remain usable
  until the game target changes. Selection changes only the view.
- Gardening account headers and their Regulars/Floaters/Empty/Pending sync bars
  use matching cyan/green colours. Character care-state colours remain independent.
- All unlockables hides category controls while enabled. Its Untradeable only
  rule takes precedence. Individual choices are retained and restored on disable.
- QuickLoot now tracks loot-state acknowledgement regardless of the native roll
  function's boolean return. This removes a path that could suppress successful
  roll toasts. Normal/quest/error test buttons are under Feedback > Messages.
  Live automatic rolling worked in .49, but Normal toasts failed for the user;
  the underlying native return is not proven to be the sole cause. Retest in game.
- Garden mapping lookup builds one index instead of scanning and sorting history
  per event. One guide draw reuses its resolved actions; they are discarded at
  the end of that draw. No cross-frame action cache or .47 projection cache added.
- Diagnostics > Tracking shows guide CPU draw time and a resettable session peak.
  This is not GPU time, full plugin update time, or total game FPS.

Validation: 820 automated checks pass; Release build zero warnings/errors.
New coverage includes physical patch-to-batch following and rejection cases,
retained global/category filter precedence, early tending restarting the full
12-hour suggestion, and 6,000-event indexed mapping equivalence. Existing
harvest, visual state, breeding/step guidance, seed/soil and sync checks pass.
Synthetic lookup fixture: previous scan 219.37 ms, indexed 10.68 ms on this runner.
These are CPU fixture measurements, not a guarantee of improved live FPS.

Live acceptance remains: toast appearance, target-follow UX, header colours,
and planting/tending/harvesting frame pacing. Confirm harvested beds show their
correct empty state and starter/replant instructions still follow the saved plan.
QuickLoot remains optional/default-off and local to this installation.
