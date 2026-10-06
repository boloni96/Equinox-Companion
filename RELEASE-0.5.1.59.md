Companion 0.5.1.59 / Journal V7.11.76 — FollowThem sessions and fleet colours

Deploy Journal V7.11.76 to the existing Cloudflare project. Both clients update
Companion to 0.5.1.59 and keep the same pairing key. No Journal reset/import.
Leader: Enable FollowThem; Share my travel with active paired followers.
Follower: select leader; enable shared portal and/or shared Teleport/aethernet;
press Start. A 45-second lease renews every 15 seconds while armed. Stop/logout
revokes it; disconnect/unload expires it. No position stream or Journal saves.
Leader checks for followers only on a travel interaction; no idle leader polling.
Signals remain 15 seconds. Active follower polls at most once/second while leader
was recently nearby; idle stopped follower has no relay polling.

Follow defaults: resume nearby ON; stop on movement OFF; stuck timeout 60 seconds,
editable 5–600; mounted takeoff assistance ON within the optional FollowThem feature.
Manual movement pauses then resumes; combat pauses. Stuck means no movement of
at least 0.5 yalms while leader is over 3 yalms away. It waits for pickup within
3 yalms. Flight takes at most three bounded attempts; no pathfinding/vnavmesh.

Travel is experimental, native validation required:
- Party offer acceptance remains separate; it does not broadcast ordinary movement.
- Public Teleport: own unlocked destination, sufficient gil, editable cost limit
  (default 1,000 gil). Shared Teleport option defaults OFF. No estate-index guessing.
- Standard aethernet: same nearby crystal, exact visible destination name, at most
  two native selection callbacks. Nonstandard transport menus are not supported.
- Public ward selection: same city crystal, observed ward 1–30, confirmed arrival,
  exact matching travel confirmation. Main ward entrance first; subdivision travel
  is a separate aethernet transition. Private estate shortcuts/world travel excluded.
- Leader/follower must have been nearby on the same world. Locked/mismatched travel
  is ignored with waiting; no treasure coffer is used as a FollowThem portal.

Submarine bars: account/group/character/submarine use worst-state priority.
Orange: observed broken part or counts below editable reserve reminders.
Yellow: idle/returned submarine or observed unlocked empty submarine slot.
Green: all recorded submarines voyaging. Grey: no fleet information.
Default reserves: 50 tanks, 10 repair kits, 20 free bag slots; local and editable.
These are reserve reminders, not calculated route costs or guaranteed loot capacity.
Repair condition is read only from loaded, installed workshop parts. Unknown data
is labelled. Slot availability uses recorded unlocked slots, never assumed four.

References: AutoRetainer VoyageUtils native part-condition approach; Lifestream
ReaderTelepotTown and residential ward interaction approach; ECommons native button
event approach; FFXIVClientStructs. Thanks to their maintainers. No new dependency.
https://github.com/PunishXIV/AutoRetainer
https://github.com/NightmareXIV/Lifestream
https://github.com/NightmareXIV/ECommons
https://github.com/aers/FFXIVClientStructs

Validation: automated policy/API/regression tests and clean plugin build; no live
FFXIV client available here. New native travel, flight and colours need tests below.

Release verification: 1,018 Companion automated checks; Release build 0 warnings/errors.
Portal/session/travel API, submarine repair roundtrip, shared-roster, Companion and
garden-plan-roster regression scripts pass. Browser UI script was not run successfully
because its Playwright browser executable is absent; native tests remain pending.
