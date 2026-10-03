# Gardening tracker source review

Reviewed 2026-10-03 for the Equinox garden state and unknown-crop work.

Source: https://github.com/Lotlab/FFXIV-Gardening-Tracker

This is a GPL-3.0 ACT plugin. Its English README describes support for the Chinese client and dependencies on OverlayPlugin/WinPcap. It is a research reference, not a Dalamud component installed by Equinox. No source code was copied in this update.

## Useful findings

- `GardeningTracker/GardeningTracker.cs` resolves crop identity from the external object's seed array, indexed using the housing link. This suggests a future way to identify an already planted unknown crop, after finding and validating the equivalent current Dalamud/client data.
- `GardeningTracker/Packets/ObjectExteralData.cs` contains eight seed entries and eight state entries. The tracker logs state bytes; the inspected implementation does not decode them into seedling/growing/wilted/dead stages. Their existence does not establish mappings for unused artwork.
- `GardeningTracker/Packets/EventStart.cs` and the main tracker distinguish inspecting, tending, harvesting and removing. Reliable bed identity and confirmed actions remain essential.
- `GardeningTracker/GardeningStorage.cs` records care and fertilizer timing. Such times support estimates; they are not proof of a currently dead crop.

Do not transplant historical Chinese-client packet offsets/opcodes into the current global Dalamud plugin. A future integration needs current structure definitions, validated seed identifiers, observation freshness, and an exact world/estate/patch/bed match. Any copied GPL code would require a license review first.

## Equinox implementation in 0.5.1.28 / Journal V7.11.41

The separate garden.dead event comes from Equinox's existing numbered English garden menu observation, only when the complete choices are Remove Crop and Quit. A healthy menu offering Remove Crop alongside tending/fertilizing does not qualify. This detection has synthetic tests but still requires in-game verification. Unknown/localized menus remain unknown.

The event preserves the observed crop and actor, uses the confirmed-dead art, and stops care/harvest estimates. Later confirmed empty/plant observations replace that state. Merely clicking the website/plugin tile or reaching an estimated timer does not confirm death. Journal protocol 9 is required before the plugin sends the new event.

Precise growth stages and flower colour/pomace mappings remain research work. The artwork inventory labels them honestly instead of claiming this repository solved them.
