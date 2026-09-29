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
