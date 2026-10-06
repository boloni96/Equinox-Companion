Equinox Companion 0.5.1.60 / Journal V7.11.77 — travel and interaction testing

Companion changes
- Settings changes enqueue an isolated snapshot. One background writer serializes and saves in order; bursts coalesce and unload flushes the latest data. The existing configuration format is retained.
- Numeric FollowThem, submarine and QuickLoot settings commit after editing. QuickLoot name searches wait 250 ms and traverse at most 250 sheet rows per frame.
- Settings > Diagnostics shows peak draw/update/snapshot time, local-save status, and reset. Actual in-game FPS improvement remains to be measured.
- FollowThem samples brief movement input each frame. A stationary follower farther than 3 yalms retries follow after 4 seconds, at most three times until progress; after the stuck timeout, visible leader movement or reappearance resumes following, without a 3-yalm pickup requirement. Visible loaded targets are used rather than a fixed 30-yalm follow cutoff. Native game range restrictions still apply.
- Mount matching uses the follower's Mount Roulette, at most three action attempts with five seconds between attempts. Existing mounted takeoff remains.
- Default teleport budget is 5,000 gil. Existing saved limits are preserved.
- Source crystal range includes its native hitbox radius. Rejected travel states now report a reason. Stop, target changes and pairing changes clear pending interactions.
- Optional Lifestream API integration for aethernet, World Visit and Data Center travel. Data Center travel has a separate explicit toggle because it can log the character out and back in. No Companion vnavmesh calls, secondary teleport or return-to-gateway request.
- Bounded exact-text SelectString/SelectYesno transport replay, only after an observed leader transition: travel NPCs, special crystal destinations and supported duty-entry menus.
- Walking housing boundaries: select the shared ward only if the follower's own HousingSelectBlock is open. No invented click target or pathfinding.
- Exact native estate ID matching against the follower's own teleport entries. Friend-list estate menu observation/replay checks the same friend content ID on the follower's own loaded Friends List.
- Additional-chambers/workshop entrance objects are included for native testing; house entry alone does not validate workshop entry.
- No-progress status changes to WAITING after two seconds rather than continuing to claim Following.
- Housing Entrance/Exit object relay after a confirmed area transition; exact object identity, nearby range, and matching confirmations. No treasure interaction through FollowThem.

Website changes
- The temporary active-session relay accepts bounded travel-menu, estate, door, World and crystal-radius fields.
- Existing pairing authentication, session expiry/revocation, replay expiry and zero Journal-event/roster writes are preserved. No Journal reset or migration is needed.

Known limits / in-game tests required
- Automated build/policy/API tests do not validate FFXIV menus or FPS. Treat these native routes as experimental.
- Unknown menu text, icon menus, Talk sequences, locked destinations, differing fees and other unmatched confirmations remain manual. This is not a claim that every ferry/Bozja/Eureka entrance works.
- Friend estate replay requires compatible standard menus and the friend to be present in the follower's loaded Friends List. An owner's private teleport is automatic only when that exact estate exists in the follower's own teleport list; otherwise it remains manual.
- Walking housing entrances require the follower to reach the boundary independently through normal follow. No obstacle navigation is added.
- Lifestream must be installed and configured on the follower. Its queues, travel permissions, service-account choice and game availability still apply. Inter-region destinations are not guaranteed; only destinations Lifestream reports as available are requested. Independent duty entry does not guarantee the same instance.
- A game process/plugin reload during a DC trip does not restore an armed session automatically. Within the same running plugin, the selected session is kept while its own Lifestream request is busy.

Credits / inspected primary sources
Lifestream optional IPC: https://github.com/NightmareXIV/Lifestream/blob/main/Lifestream/IPC/IPCProvider.cs
Lifestream travel scheduling: https://github.com/NightmareXIV/Lifestream/blob/main/Lifestream/Tasks/CrossWorld/TaskTPAndChangeWorld.cs
Native game definitions: https://github.com/aers/FFXIVClientStructs
Dalamud configuration writer: https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Configuration/PluginConfigurations.cs
Thanks to these projects for documented APIs and implementation references. Companion remains a separate plugin.
