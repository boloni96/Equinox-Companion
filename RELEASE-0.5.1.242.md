# Companion 0.5.1.242

The actual first-launch welcome uses Goddess blue, with no example Adventure. Keep Goddess blue or use classic Dalamud: both choices are saved and open Companion. Existing pairing and appearance preferences remain intact. Closing the welcome retains the saved style; Settings → Appearance can reopen it.

Both designs use the same feature registry, section renderers, sync and gameplay logic. Section performance reporting now follows the same path in both layouts. The shared window launcher keeps Adventure, housing bids, item tools, gardening, Fashion Report and testing accessible in either style.

Release checks: gate tests, native text tests and the production plugin build run before the installer feed advances. In-game visual checks are still required at narrow widths and increased font scales.

## Ongoing design parity

New feature sections belong in CompanionTabs; feature bodies stay shared. Style changes belong in appearance/layout code, never in observation, sync or garden mapping code. Test blue and classic for every affected feature. Neither style is a legacy feature edition.
