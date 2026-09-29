# Equinox Companion 0.1.0 — first in-game test

This is an early Dalamud plugin for Equinox Journal. The first milestone is a
local observation recorder, **not the finished automatic website integration**.

## Included

- Automatic, locally saved housing observations with character, house ID,
  world/territory IDs, ward, plot and time.
- Debounced entry detection. Loading the plugin while already inside a house is
  labelled `house.observedInside`; it does not invent an entry time.
- An optional five-minute gardening recorder: target/furniture identifiers,
  selected planting item IDs and numeric game log event IDs/parameters.
- JSON export from `/equinox`, capped at 500 house observations and 2,000 garden
  diagnostic records. These diagnostics remain in memory until exported; house
  observations are saved in the plugin's normal configuration.

There is no website connection, automatic planting/watering checkbox update,
verified garden-slot mapping, or demolition-reset confirmation in this version.
Garden events are explicitly unconfirmed candidates. The recorder does not
perform game actions, collect player chat text, or send data online.

## Install from GitHub through Dalamud

The test build is hosted directly in this repository.

1. In FFXIV, open `/xlsettings` → **Experimental → Custom Plugin Repositories**.
2. Add and enable this repository URL, then save:

```text
https://raw.githubusercontent.com/boloni96/Equinox-Companion/main/repo.json
```

3. Open `/xlplugins`, search for **Equinox Companion (Test)**, and install it.
4. Type `/equinox` to open the plugin.

Dalamud downloads the plugin from GitHub. No local DLL path is needed. Keep this
same repository link for updates. Penumbra is not involved.

If you loaded the old development DLL, disable/remove its Dev Plugin Locations
entry before installing the GitHub copy. Keep your plugin configuration.
If Dalamud reports an API mismatch, stop and report it; do not override the
compatibility check.

## First test

1. Start outside a house, wait two seconds, enter it, and wait two seconds.
   Check the displayed ward/plot against the in-game address.
2. Exit and enter a second house. Check that a separate observation appears.
3. Return to an outdoor garden. Click **Start a 5-minute garden test**.
4. In a known patch/bed, select seed and soil, then cancel once. Then plant for
   real. Tend that plant, then tend a different bed. Optionally fertilize or
   harvest if you already intend to do so.
5. Click **Stop recording**, then **Export test JSON**. The plugin displays the
   file path and provides a copy-path button.
6. Send the exported JSON with a short note of the real address and which beds
   you used, in order. It contains your character names/IDs and house addresses.

Repeat the garden test after relogging and with a second garden patch when
available. We need evidence that garden/bed matching survives zone changes and
that cancellation cannot be mistaken for planting. The recorder does not yet
claim to know the bed number. Short sessions are easier to inspect.

## Build from source

Use .NET 10 and an API-15 Dalamud installation. The project follows the official
Dalamud sample's `Dalamud.NET.Sdk/15.0.0` configuration.

```powershell
dotnet build src/EquinoxCompanion.csproj -c Release
dotnet run --project tests/GateTests.csproj -c Release
```

The SDK normally resolves Dalamud through XIVLauncher. For a separate reference
folder, set `DALAMUD_HOME` to that folder before building. Never commit or package
the game/Dalamud dependency DLLs with this plugin.

## Validation and limitations

The release notes in `BUILD-RESULTS.txt` describe the build actually performed.
The transition tests exercise startup inside, repeated observations, transient
loading, genuine revisits, character changes and unknown doorway transitions.
Native game observation and UI still require an in-game test on Windows. A
successful compilation does not establish that every observed address or
gardening identifier is correct. Game patches can require rebuilding/updating
the native structures.

## Next milestone: connect confirmed observations to the journal

The inspected journal baseline is `EquinoxJournal-V7.9.10-Source.zip`.
See `INTEGRATION.md` for the current data-model mapping and sync requirements.
