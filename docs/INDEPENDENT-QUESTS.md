# Independent quest execution and local collision navigation

Companion now bundles the complete pinned quest-data pack and executes supported steps internally. The runtime does not call or load Questionable or vnavmesh. Lifestream remains the existing integration for aethernet/world/account travel.

## Implemented

- Lossless, deterministic import of 4,388 routes and all 4,392 QuestPaths JSON files from `f737a0865627bc6fa70fb7024ab403b3e4881736`. Original bytes, author fields, licence and symbolic travel/emote source mappings are preserved. Corresponding source is the pinned `vendor/questionable-data-source` submodule.
- Standalone route reader with per-route hashes and unknown fields preserved.
- Live FFXIV BGCollision queries for ground, floor normals, headroom, body clearance and supported edges. Native queries stay on the framework thread.
- Incremental local A* search, budgeted per framework update. Separate heights are separate cells; every edge is checked. Maximum range 160 yalms, bounded search, no straight-line fallback. Flying and mounted movement are not supported.
- Companion movement through UI-filtered forward input, with character/camera heading updates. Input leases expire after 180 ms unless renewed. Manual movement, Stop, unload, dialogue, text entry, loading, combat and duty release or suppress the synthetic input. Body checks are refreshed at each waypoint and at most every 150 ms between waypoints. Stalls allow two new route searches before a clear stop.
- Event quest/account NPC approaches use this walker rather than Lifestream.MoveEx.
- General quest execution handles basic ground approaches, NPC interactions, accept/complete, verified Talk, localized matching Yes/No and unprompted list answers, quest item use, emotes, localized Say phrases, and completion variable flags.
- Quest-line continuation and other accepted quests now use the independent runner and bundled route review. Unsupported steps remain pending with a reason, allowing another accepted quest. Duties and solo duties are manual.
- Reports include navigation state, waypoint, goal, search count, replan count and quest sequence/step/action state.
- CI tests full data preservation, deterministic packing, independent reading, travel/emote mapping, bounded/cancellable path search, collision corridor routing, disconnected floors, unknown-condition rejection and quest-variable matching. Full plugin compile also passes.

## Practical limits and live validation

This is local collision-sampled ground navigation, not a prebuilt full-zone mesh. It can fail in narrow passages, steep surfaces, long routes, unloaded collision regions and unusual geometry. Replanning is bounded. Its frame budget is checked between edge queries; an individual native raycast cannot be preempted.

The entire catalogue is imported, but not every quest is automated. Unsupported conditions (including complex required-variable/skip predicates), combat, crafting, gathering, fishing, jumps, special UI minigames and other unimplemented interaction types are left pending. List choices with an additional prompt require manual input. Interactions are sent once and await observed progress; they are not blindly retried.

Cross-zone steps need a supported unlocked aetheryte shortcut or manual travel. Aethernet integration is used only beside an available crystal. The dedicated Saucer event route retains its existing teleport/aethernet/entrance handling.

Automated build/test success does not establish in-game route correctness. Gold Saucer and Keybound Square still need live checks for walls/stairs, input steering, zoning, dialogue ownership, manual interruption, completion and the reward/account flow. No live success is claimed from synthetic collision tests. Manual recordings remain observations, not automatic route replay.

Upstream data: https://github.com/PunishXIV/Questionable (AGPL-3.0, included in the data pack). No vnavmesh source is copied. Collision APIs are supplied by FFXIVClientStructs; the runtime and planner here are independently implemented.
