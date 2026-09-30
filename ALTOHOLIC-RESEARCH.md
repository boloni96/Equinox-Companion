# Altoholic research — 2026-09-30

Reviewed source: https://github.com/Sohtoren/Altoholic (main), especially Altoholic/Plugin.cs, Models/Character.cs, Models/Housing.cs and Helpers/Event.cs. An older DreanorFFXIV repository was also inspected but is not the housing-capable implementation. This is an API/behavior review; no Altoholic source was copied into Equinox.

## Findings used now
- Altoholic GetHousing gates on home world, indoors and house permissions, stores HouseId/address and updates LastCheck while inside. Treat this as last observed inside, not recovered entry history. Equinox preserves its doorway event gate.
- Use stable character content IDs after first name + home-world match. Home and visiting world stay separate. Jobs and character state can be read locally; send changed snapshots rather than timer heartbeats.
- Housing access does not prove personal ownership. Equinox additionally compares the native owned estate ID, and checks the matching FC member list before labeling an FC estate.
- FC name and estate name are separate. Equinox uses the native housing signboard response for estate name and size, with an address match.

## Follow-up candidates, not implemented in this release
- Events: Altoholic maps specific quest IDs to completed event quests. We need an explicit quest-to-journal-event mapping; an old repeated event quest must not automatically complete a new event run.
- Rewards: separate unlocked collections (mounts, minions, emotes, rolls, hairstyles, etc.) from current inventory possession. Missing inventory is not proof a reward was never acquired. Retainer/saddlebag/dresser data may need those windows opened and timestamps.
- Fashion Report: Altoholic records Masked Rose scene data, highest score, allowances and observation time. Equinox needs weekly-cycle validation, including Tuesday/Friday boundaries, before checking the website task.
- Retainers, currencies, weekly allowances, Wondrous Tails and custom deliveries are additional candidates. Keep collection opt-in and send only fields with a concrete journal purpose.
- Gardening: this reviewed source does not supply a ready-to-use per-bed planting/harvest synchronizer. Continue using Equinox's captured selection plus confirmed game result.

## Remaining in-game validation
FC member info can be unloaded/partial; open the member list with your own character visible. Placard details require opening the owned estate placard. Verify the new native planting selection observer in-game, including cancellation and fast YesAlready confirmation. Harvest/item messages alone remain insufficient to clear a bed.
