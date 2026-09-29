# Version 0.1.4.0

- Observe AtkUnitBase.FireCallback while recording, using the API 15 client-struct function pointer. Forward original arguments and return value unchanged; never initiate a callback.
- Limit diagnostic capture to an active SelectString garden menu with matching actor, property, recent target, addon address, bounded arguments, and a one-minute menu expiry.
- Preserve menu options and title; export numeric callback arguments and the first-argument option candidate. Submitted options do not yet confirm successful watering.
- Show observer availability and last submitted option. Disable observation on stop and dispose the hook on unload.
- Keep website sync disabled pending in-game validation.

Validation: Release build has zero warnings/errors; 31 automated checks pass. Native callback capture and Yes compatibility require the next in-game sample.

# Version 0.1.3.0

The 0.1.2.0 SelectString-only listener produced no menu records during manual testing. This diagnostic update broadens observation to HousingGardening, SelectString, SelectIconString, ContextMenu and SelectYesno, including setup, refresh, receive-event and finalize notifications.

- Record bounded text/numeric menu values and event parameters without requiring a valid popup-list index. Do not cast other menus to SelectString.
- Only collect while recording near a fresh garden target; skip pointer/vector values and hover noise. Opening or selecting a menu is not classified as successful watering.
- Display a menu observation count so the next test can be short.
- No gameplay actions, network sync, or changes to website data.

Validation: API 15 Release build with zero warnings/errors; existing 22 tests pass. Menu capture still requires in-game verification, including compatibility with confirmation plugins.

# Version 0.1.2.0

- During opt-in recording, copy garden EventObj argument and timeline state from the supported client struct. These are raw diagnostic values, not decoded bed IDs.
- Observe SelectString list click/select events near a recently targeted garden object. Record the selected menu label, index, event type, and candidate target. Menu selection alone does not confirm action success.
- Menu listener is read-only and removed on plugin disposal. Menu capture is limited to the garden object base ID observed in testing, valid indices, and bounded queues.
- No website updates or automatic gameplay. Patch/bed mapping and watering success remain under investigation.

Validation: API 15 Release build, no warnings/errors; existing 22 checks pass. New native/menu diagnostics require in-game validation.

# Version 0.1.1.0

- Copy garden context when a game log arrives, before the next framework update can clear it.
- Retain a recent target for up to two seconds, with timestamps and an explicit unverified association. Clear context on zoning, recording reset, character changes, and property changes.
- Sample every 50 ms while recording and export target kind, base ID, entity ID, and position for bed-mapping investigation.
- Label planting, fertilizer, removal, and status signals. Item receipts are not automatically classified as harvests; healthy-crop messages are not proof of watering.
- Show the latest six recognized signals in the recorder. Export schema is now version 2.
- Local diagnostics only. Exact bed mapping and website synchronization remain pending.

Validation: .NET 10 / Dalamud API 15 build succeeded with no warnings or errors; 22 observation/correlation/classification checks passed. In-game validation remains required.

# Equinox Companion — first test build

Local house observations and an optional gardening diagnostic recorder for
Equinox Journal. Requires Dalamud API 15.

This is an early test build. Native game behaviour still needs in-game
verification. Website sync and confirmed planting/tending/harvesting updates
are not implemented yet. Type `/equinox` after installation.

The custom repository list and install ZIP are attached to this release.
