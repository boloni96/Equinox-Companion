Equinox Companion 0.5.1.41

- Fashion Report completion now adds a green border to the header shortcut and minimized icon. The border and checkmark use the same current-character event window: Friday 08:00 UTC through Tuesday 08:00 UTC, with both removed exactly at the end. The hover keeps Fashion Points and adds the opening/end times. Newer shared score observations can supersede older local completion.
- AutoRetainer's existing read-only integration now imports free character inventory slots separately from submarine capacity, alongside Ceruleum Tanks, Magitek Repair Materials, vessel timers, parts, rank, experience and routes. The saved GitHub research was recovered and checked against upstream before extending the implementation.
- Direct current-character supply observations read the four loaded carried bags. Counts synchronize on change and are reconfirmed after five minutes of real sampling. Unloaded inventory does not become zero. These readings carry an actual observation timestamp and are kept separately from cached values whose game observation time is unknown.
- Submarines covers every paired person's accounts, with collapsed groups and character sections, return timers and supply hovers. Explicit refresh rescans the available cache and paired roster. A failed individual cache record no longer aborts the remaining character scan.
- Per-character supplies remain separate even when characters share the same FC fleet. Shared projections exclude characters who have left or changed FC. Newly imported cache does not overwrite dated direct observations.

Requires Journal V7.11.52 for direct supply synchronization (protocol 13). Upload the website, refresh/save the paired Journal, then update Companion. Background cached import is controlled by Settings > Tracking. It can read existing offline data; it cannot refresh game state for an offline character.

Validation: 693 automated plugin checks; release build with zero warnings/errors. Website checks cover two persons with main/alt accounts, same-name characters on different worlds, per-character supplies, shared FC fleets, zero counts, inventory bounds, stale/departed-FC records, authenticated event cleaning, live/shared projection, countdown boundaries, collapsed layouts and mobile overflow. Existing gardening/collections/Fashion browser regression checks passed.

Native inventory sampling, AutoRetainer IPC dispatch and the in-game icon rendering still need an in-game check. Compare one character's tank/repair counts and free inventory slots after updating, and inspect the Fashion hover on a completed character.

Official Fashion schedule: https://na.finalfantasyxiv.com/lodestone/playguide/contentsguide/goldsaucer/fashionreport/
AutoRetainer source details and pinned revisions: AUTORETAINER-RESEARCH.md.
