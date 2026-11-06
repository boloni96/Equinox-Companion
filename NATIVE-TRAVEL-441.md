# Companion 0.5.1.441 — native travel test build

World travel commands `/et world <name>`, `/et w <name>` and `/etw <name>` share the native route. The existing Goddess loading indicator shows progress; hover shows details; right-click or `/et stop` cancels remaining Companion work. There is no separate travel window.

## Implemented routes

- Main-city World Visit: unlocked native teleport, existing collision-aware approach, localized aetheryte menu entry, exact world selection, destination confirmation and verified arrival.
- Cross-DC visit: current-character identity and service-account capture, logout, native character context menu, destination DC/world tree selection, identity/destination validation, transfer completion, service-account selection and login. Visitors return home before requesting a different DC. No alternate destination or account is silently substituted.
- Native aethernet: verify unlock, approach a shard in the right group or teleport to its main crystal, select the destination through `TelepotTown`, verify arrival.
- Automatic callers migrated: shopping, Adventure Bring, estate home-world continuations, event-account world/return travel, delivery home return and follower world/DC travel.
- Shopping retains world → city → optional shard as one request. It waits for a stable ready interval and retries transiently refused city teleports without requiring a second click.

## Diagnostics

All diagnostic export variants include `travelPrompts`: a bounded local 600-record history with UTC/sequence, menu lifecycle, click/change identifiers, numeric callbacks, text/buttons, identity/account changes, and travel stages/actions. A dropped-record count makes truncation explicit. Relevant menus are sampled in small measured batches and recorded on changes. Captures work while logged out and during manual travel without arming FollowThem. This is not a capture of every game UI surface or every native event payload. Private chat, arbitrary text input and credentials are not recorded. `shoppingJourney` adds current trip/status to the main export.

## Test in game

1. Same DC: `/etw <available other world>` from outside a gateway city. Verify one request teleports, approaches, selects the exact world and arrives.
2. Cross DC: `/et w <available world on another DC>` with a known service account. Verify character identity and destination selection, confirmation, service-account prompt and login queue. Queues must not be cancelled automatically.
3. From a visiting world, request a different DC and verify home return occurs first; verify the same character/account throughout.
4. `/whereischeap`: select a different world, shopping city and shard; click Travel once and wait through all stages. Repeat when already on the destination world.
5. Right-click the Goddess indicator during approach, destination selection and a queue. Movement and subsequent Companion actions stop. An already committed game transfer can still finish.
6. Try unavailable/unattuned destinations and unexpected prompts. No arbitrary confirmation, character switch or alternate destination should occur. Export diagnostics after a failure, before repeated tests displace old records.
7. Manual travel with FollowThem unarmed: export after returning to game; confirm the service-account and transfer prompts appear in order.

## Remaining limits

Live game verification is still required for native menu event compatibility. Unrecognized localized confirmations are left for manual confirmation, and their text is recorded. World/DC availability and queues are controlled by the game. Optional legacy friend-estate/address and captured-position approach integrations still exist; this release does not remove Lifestream from every optional feature.

Interop field/menu contracts were checked against FFXIVClientStructs (aers/FFXIVClientStructs) and the public Lifestream native menu readers/schedulers (NightmareXIV/Lifestream). Companion uses its own state machine, existing navigation, game UI structures and menu events; the migrated routes do not invoke Lifestream travel IPC.
