# Independent quest and navigation work

Goal: execute imported quests in Companion without installing Questionable or vnavmesh. Use an internal collision-derived 3D navigation system; retain aethernet travel. Duties and solo duties remain manual.

## Implemented on this development branch

- Pinned upstream source as a data-source submodule at `f737a0865627bc6fa70fb7024ab403b3e4881736`.
- Lossless deterministic importer: 4,388 routes, all 4,392 QuestPaths JSON files, original authors and AGPL-3.0 licence. No Questionable executable is built or loaded by this importer/reader.
- Independent route reader verifies per-route hashes and retains the entire JSON, including unknown fields. Identifiers are strings so custom routes are not discarded; duplicate IDs remain selectable by source path.
- Missing-handler checks explicitly distinguish imported data from executable support.
- Independent A* search over connected 3D walkable cells and shared portals, including one-way connections, cancellation and bounded search. Disconnected floors return no route. This component consumes validated geometry; it does not generate it.
- CI verifies every source JSON byte, deterministic packing, route reading, aethernet preservation, unsupported actions, corridor routing, disconnected floors, cancellation and plugin compilation.

## Still required before release

1. FFXIV scene/collision extraction into immutable geometry on the game thread, with mesh generation off-thread and territory/version-aware caching.
2. Clearance, slope, step-height and floor separation during mesh generation; no straight-line fallback through unverified geometry.
3. Internal movement controller with progress observation, manual interruption, arrival checks, bounded recovery and stop reasons. Existing Lifestream movement is not an independent movement implementation.
4. Quest executor adapters for conditions, quest variables, dialogue choices, interactions, item actions, travel and sequence completion. Reject unsupported semantics before issuing an action; duties stay pending for manual play.
5. Connect the independent book/executor to quest library and accepted-quest continuation, replacing Questionable IPC only when supported.
6. Live validation in Gold Saucer and Keybound Square, including aethernet, walls/stairs, zoning, dialogues, cancellation and account reward flow.

The released 0.5.1.379 general quest runner still uses Questionable IPC. This branch is a development foundation, not a completed replacement and not an installer release. No game geometry extraction or autonomous movement is claimed as implemented here.

Upstream: https://github.com/PunishXIV/Questionable. Original route JSON is retained without modifications; its licence and author fields travel with the generated data pack. No vnavmesh source code is copied.
