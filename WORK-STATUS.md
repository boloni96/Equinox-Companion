# Equinox Empire — implementation and live-validation status

Journal V7.11.0 / Companion 0.5.1.0, 2 October 2026. Updated against all 23 requests and later corrections, including numbered storage locations.

Implemented means source changes and relevant local checks, not proof of live FFXIV behavior. This remains an in-game validation release.

| # | Implementation | Validation or remaining boundary |
|---|---|---|
| 1 | Game estate names take precedence, private owner/FC member entry eligibility, paired placards and Company Profile master identity | Packet/profile observations need live testing; missing FC data does not prove departure |
| 2 | Daily gardening route, grouped manual corrections, observed planting/tending/maturity and empty numbered beds | Reopen a bed after harvest; no-permission messages require previously calibrated target/bed mapping |
| 3 | Small idempotent batches, fixed database query count, compact background roster, independent readers, storage protocol negotiation, retained pending actions | Production Cloudflare CPU must be measured; full journal/gardens still apply with website open |
| 4 | Character/account navigation and history integration | Browser navigation checks pass; nested real-use feedback remains useful |
| 5 | Unified timers; Fashion completion from actual judged participation, with score shown and no 80/100 selector | Native Fashion and timer observations need live validation |
| 6 | 3,116 unlock catalogue entries plus 29,057 equipment entries; single Obtained filter; Not learned marker; hairstyles restored; cached personal storage and separate shared FC pages | Equipment sources not fully classified; missing means not found in recorded storage, not proof every unopened container is empty |
| 7 | Catalogue events on all accounts, compact Hide/Show, simplified expanded actions, year-specific history from Altoholic | Reused quest bits cannot establish an old rerun year; ambiguous mappings stay unconfirmed; complete access rules are not available for every activity |
| 8 | Single Rename Person control in left sidebar; style preview name read-only | Browser verified |
| 9 | Minimum plugin size and native pin/clickthrough/blur controls | Requires in-game interaction test |
| 10 | Game-controlled character details, compact jobs with unchanged icon size, FC page, cached exact-name/world Lodestone lookup and portrait fallback | Official-site outages leave lookup unconfirmed; personal notes and portraits remain user-controlled |
| 11 | Companion character-to-website links | Requires signed-in user's browser for private journal |
| 12 | Reward/collection/job game icons; approved plugin artwork normalized to 512px and versioned manifest/feed icon URL; website logo moved below footer copyright | Remote collection icons are optional online assets; not an offline icon cache |
| 13 | Per-person revocable selection, individual owner-selected photos, token-scoped image access, moderated Guest Welcome Book | Snapshots change on owner publication; no silent public exposure of later uploads |
| 14 | 60-day acknowledged history pruning; latest state per container and unsent actions retained | Long-running live growth still needs observation |
| 15 | Revised status, storage, Fashion, estate, version and guest copy | No claim an unknown native observation is confirmed |
| 16 | Dedicated Submarines plugin tab, newest FC observation, website timers and compact background voyage propagation | Workshop capture is passive and requires loaded data; live test outstanding |
| 17 | Collection/quest/reward/Fashion automatic observations, year-aware quest history and repeat transitions | Every reward and repeated event cannot be inferred safely; unknowns remain unconfirmed |
| 18 | Wishlist follows learned/held collectibles and recorded equipment, with icons and Not learned marker | Exact unique name or item/key match required; ambiguous names are not guessed |
| 19 | Same pairing key, normalized startup, acknowledgments, retries, held records isolated and logged once per session | Two-client contract tests pass; live reconnect test outstanding |
| 20 | Green entry with paired owner/FC context; daily red 30-day eligible-entry reminders | In-game color/threshold check; demolition countdown itself remains game-owned |
| 21 | Automatic unique name/home-world matching and creation/linking of verified owned/paired-known estates | Unknown characters require a person/account assignment, avoiding invented ownership |
| 22 | Authoritative placard names and matched FC profile/master details | Reproduce supplied scenarios in game on this version |
| 23 | Hashed cloud-picture explanation, previews and reference-safe cleanup; guest-selected photos protected too | Cleanup race, revocation and guest authorization tests pass |

Storage labels report Inventory page, Armoury equipment section, Glamour Dresser, Armoire, retainer name and inventory page, or shared FC Chest page. Unloaded containers never become empty snapshots. FC Chest availability never marks personal ownership.

Verification: .NET build zero warnings/errors; plugin gate tests; cloud/auth/storage tests; companion observation tests; roster and two-client tests; desktop/mobile browser collection, unused-item marker, jobs, FC navigation, event visibility, guest photo selection and message tests; clean public seed and hosted/offline consistency checks. Real game and production Cloudflare validation cannot be run in this environment.

Delivery uses the existing GitHub installer feed. Website ZIP is for the existing Cloudflare Pages deployment, preserving DB/PICTURES bindings, password, key and journal. Deploy website first and save once to advertise protocol 3. Nothing here rotates the key or replaces user data.

References consulted: [Altoholic](https://github.com/Sohtoren/Altoholic), [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs), [SubmarineTracker](https://github.com/Infiziert90/SubmarineTracker), [AutoRetainer](https://github.com/PunishXIV/AutoRetainer), [FFXIVGachaSpreadsheet](https://github.com/Infiziert90/FFXIVGachaSpreadsheet), [FFXIV Collect](https://ffxivcollect.com/), [game-sheet extraction](https://github.com/xivapi/ffxiv-datamining), [DalamudPackager](https://github.com/goatcorp/DalamudPackager). Reference projects inform passive observation and factual mappings; Equinox does not require them installed or trigger their automation.
