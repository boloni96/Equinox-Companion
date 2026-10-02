# Companion 0.5.1.9 — Account discovery and MSQ grouping

Requires Journal V7.11.17 for new account/progress fields. Log in once on a known character from each service account while the paired journal is open. Its journal-scoped account fingerprint learns the existing person/account destination; subsequent unknown characters sharing that identity are added automatically. Conflicting or unseen identities wait for linking instead of guessing. No raw game account ID or session data is transmitted.

Regulars have completed the level-15 MSQ “It’s Probably Pirates” (either game-data variant). Below that milestone, FC members are Floaters and confirmed nonmembers are Empty. Unloaded checks remain Pending sync. This is a progress baseline, not proof of purchasing a boost or universal seasonal-event/Fashion eligibility. Manual eligibility flags remain unchanged. Existing markers remain fallback until a game observation is available.

Live progress/FC observations refresh paired roster groups without a browser save. Adding new characters to the saved journal still requires the signed-in website open with automatic companion application enabled. Compile and automated mapping/grouping checks passed; account identification and observations require an in-game check on both users’ service accounts.

# Companion 0.5.1.8 — Private, FC, Shared house order

Expanded character estates now use [Private], [FC], and [Shared], ordered Private first, the character's FC second, then all shared estates in their existing relative order. Shared applies to private and FC estates. The underlying estate type and access explanation remain in the hover details. Shared rows no longer have a repeated warning line above their name. Colour bars exclude shared estates. Journal V7.11.16 publishes character/estate FC identities to distinguish the character's own FC from a different shared FC. Older caches can identify FC masters; other unverified FC relations temporarily show Shared until the new roster is saved.

# Companion 0.5.1.7 — Account groups and character refresh

Keeps each person as a tab. Each account expands/collapses and remembers its state. Inside are Regulars (boosted/ready, including no FC), Floaters (boost needed and FC member), and Empty (boost needed without FC membership). Uses Journal boost-needed/eligibility markers, not proof of a purchased boost. Search includes account names and temporarily opens matching accounts. Existing per-character housing colours and ordering remain within their groups. Journal V7.11.15 supplies stable account IDs and group metadata; older cached data falls back to ~ names and linked FC estates.

Game character observations refresh on login and hourly even if unchanged, in the background. Actual changes still send sooner. Includes pending 0.5.1.6 Fashion Report picture zoom/pan and default tab order. Native in-game layout/observation testing remains necessary.

# Companion 0.5.1.6 — House dividers and base tab order

Fashion Report supports mouse-wheel zoom (1–8×), left-drag panning, cursor-centered zoom, bounded panning and Reset view. Click without dragging still expands/shrinks the window.

Adds a horizontal divider between estate detail groups in local and shared person views. Base order: Tests, Characters & housing, person tabs in journal order, Submarines, Settings. Existing saved custom order remains intact. New person tabs are inserted before Submarines.

# Companion 0.5.1.5 — Persistent tab order

Other visits now has its own scroll area, capped at six text rows, with newest visits first and a count. No visit records are deleted.

Command renamed to `/fashionr` because the game already uses `/fashion` for fashion accessories. `/equinox fashion` remains available.

Settings is last by default, after person tabs. Dragged tab order is saved to plugin configuration after releasing the mouse and restored after restart/update. Stable person IDs preserve order across renames; temporarily missing people retain their saved positions. New tabs start before Settings. Existing saved custom order takes priority.

# Companion 0.5.1.4 — Fashion login refresh

Checks the shared Fashion Report picture on character login and once per hour while logged in, even with its window closed. Login does not open the window or print the chat link. `/fashion` opens the in-game picture and prints the optional browser link. Keeps the existing picture on failure. Native game testing still required.

# Companion 0.5.1.3 — Fashion picture window

`/fashion` opens the current V1 report inside the game and prints a clickable browser link in local chat. Click the image to expand/shrink; resize, pin, refresh, or open in browser. Week/theme are read from Fashion Report XIV metadata. Refreshes every 15 minutes while open and when reopening a stale picture. Downloads run asynchronously; previous image retained if refresh fails; textures released on replacement/unload. `/equinox fashion` is the fallback if another plugin owns `/fashion`. Does not mark Fashion complete. Published through the normal repo.json installer feed; user will test native behavior in game.

Local compilation passed. Actual game rendering, click sizing and chat link still require testing in Dalamud.

# Companion 0.5.1.2 — Company Profile sync

Reads the separate Company Profile window opened from an estate placard, as well as the existing FC Members source. Sends master, rank, member count, founding date, slogan, Grand Company, activity, focus, recruitment and estate name when observed. Matches a viewed profile to a recent estate placard without assigning the visiting character FC membership. Preserves unsigned 64-bit FC/Lodestone identity.

Deploy Journal V7.11.11 first, sign in and save once, then refresh shared profiles in Companion. New Company Profile events stay local until protocol 4 is advertised. Existing normal observations continue syncing with older supported website versions.

Local build and sync/identity tests passed. Native game behavior remains to be validated: open the placard and its owner magnifying glass, keep Company Profile open at least five seconds, then check the paired FC/estate on the website. Viewing a stranger must not create membership or reset an entry timer. The blue installer icon is packaged but its display on the user's machine remains unconfirmed.


# Companion 0.5.1.1 / Journal V7.11.1

This is an in-game validation release. Build and automated checks pass; native game observations still require live verification.

Deploy Journal V7.11.1 to the existing Cloudflare Pages project, open and save it once, then refresh shared profiles in Companion. Keep both users on the existing pairing key. Storage events are held locally until the website advertises protocol 3; existing supported events continue syncing.

- Restored the missing hairstyle catalogue and added release checks so hairstyles cannot silently disappear again.
- Remember loaded personal inventory, equipped gear, Armoury Chest, saddlebags, Armoire, Glamour Dresser and individual retainers. Unopened containers are not cleared. Retainer and FC Chest observations require a loaded, identified context.
- Locations include inventory page numbers, retainer names and retainer inventory numbers. Shared FC Chest pages are displayed separately from personal ownership.
- Dedicated Submarines tab uses the newest observation per FC. Header status sits beside the approved icon.
- Same approved icon normalized to 512×512; compiled manifest and feed include IconUrl. Original artwork remains in assets/companion-icon-original.png.
- Incomplete login records are logged once per session, retained for diagnostics and never allowed to block valid actions.
- Current storage snapshots survive the 60-day acknowledged-history pruning. Storage writes are batched to avoid repeatedly writing the full config during one observation pass.

The website adds equipment/clothing, one Obtained filter with Not learned markers, compact job rows, game-observed Fashion completion, automatic exact Lodestone matching, event history by year, all-account catalogue coverage, per-photo guest sharing, a moderated Guest Welcome Book, and protected photo cleanup.

Live checks: update both clients; open each storage location and retainer; move an item between inventory and retainer then use/unlock it; verify per-character isolation after switching characters; read private/FC placards and Company Profile; open a numbered garden bed without harvest permission; speak to Masked Rose after judging; open workshop voyages; verify paired green entry notices/red 30-day reminders. Housing times remain recorded-entry estimates; game demolition status is authoritative.

Equipment sources are not yet fully classified. Unknown/ambiguous quest and reward mappings remain unconfirmed. Reused quest bits cannot prove which past rerun was completed. Unlinked characters still require a person/account assignment. Full journal and garden application requires the website open; compact known-estate and voyage updates can reach paired plugins in the background.

Blue branding patch: sapphire Companion icon with silver highlights; Journal uses the matching blue book icon.
