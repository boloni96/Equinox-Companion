Equinox Journal V7.11.38 / Companion 0.5.1.24

Garden artwork and guidance
- Care overlays are bottom-right inside bed borders. Original top-right status icon remains the sole hover/focus/tap information target.
- Captions leave room for the care badge. Independent match/difference/fertilizer annotations and starter/replant badges now select supplied artwork.
- State icons now map water, tend, ready, keep-mature, uncertainty, wilt and death estimates. Additional metadata icons identify observations, seed, maturity, fertilizer, status, warnings and tips where relevant.
- Wrong opened bed has a red border and warning icon; the correct next bed has the next outline and NEXT label. Both guidance views now share the same next-step calculation. Physical bed numbers never change.
- Unidentified crops explain how to inspect the numbered bed in game, with manual correction if the game does not reveal the identity.
- Confirmed Remove Crop success (ordinary or withered removal) records the exact numbered bed as empty, clears its care/fertilizer timers and syncs it. Cancel, denied removal, wrong target/house/actor, stale context and unrelated messages cannot clear a bed. Planned instructions can remain ghosted while the actual bed is empty. Harvest still requires reopening the numbered bed to observe empty; item acquisition is not treated as harvest proof.

Research and remaining artwork
The game LogMessage table has doing-well, fertilizer, removal and pomace messages. No precise outdoor seedling-to-growing stage was established from those messages. The documented AgentHousingPlant fields describe item selection, not a growth percentage; HousingFlowerPotObject has no documented growth field.
https://github.com/xivapi/ffxiv-datamining/blob/master/csv/en/LogMessage.csv
https://ffxiv.wildwolf.dev/api/FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentHousingPlant.html
https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/Object/HousingFlowerPotObject.cs
The existing opt-in five-minute garden diagnostic recorder now captures other system-message text associated with the current outdoor garden target, not just the mature-crop sentence. Diagnostics stay local until explicitly exported. They do not invent crop stages or upload chat text.
Seedling stages, confirmed death/wilt, storage pause and flower colour remain dependent on reliable observations or a supported explicit correction flow. Flowerpot tracking/colour mapping and icon-only native seed/soil selector highlighting are not completed here. Wet composites and legacy live pictures are alternatives to the active layered art, not missing required states. Reference atlases/ORA files are authoring sources, not runtime pictures.
All 1,565 installed PNGs remain available in the artwork preview. The remaining inventory excludes files wired in both apps; it does not claim every branch has been verified in game.

Validation
Plugin compiled and garden logic tests pass, including removal correlation, rejection cases, wrong-soil correction, next-step movement, starter removal/replant and maturity protection. Website desktop/mobile custom-theme layout, image loading and top-right-only hover/focus/tap tests pass. Hosted/offline parity and clean public seed checks pass. Native appearance and successful removal capture still require your FFXIV test.

Install
Upload the Cloudflare ZIP inside this package to the existing Cloudflare Pages project. Preserve existing bindings, database and pairing configuration. Website not deployed by the assistant.
Source ZIP contains reviewed source and optional standalone offline page. Companion 0.5.1.24 is published separately to the existing GitHub installer feed.
