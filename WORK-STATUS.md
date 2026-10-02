# Equinox Empire — 23-point audit

Release: Journal V7.10.2 / Companion 0.5.0.2, 2 October 2026.

**Conclusion: the recovered work is substantial and the release can be tested, but all 23 points are not fully complete.** “Implemented” below means code exists and relevant local checks pass. It does not mean live FFXIV or Cloudflare production behavior has been verified.

| # | Request | Verified implementation | Still required |
|---|---|---|---|
| 1 | Shared placards, game names first, FC name/master, eligible entries | Paired estates, authoritative names, master matching, ownership-change protection; placards never create interior visits | Native packet/profile test; departed-FC membership without new verified FC details |
| 2 | Simple automatic gardening | Daily stops, per-bed batches, planting/tending/maturity, calibrated no-permission observations, observed empty-bed sync | Reopen bed menu after harvest; immediate harvest-action detection and live target calibration |
| 3 | Free-plan CPU, sync, queues and refresh | Smaller payloads, compact roster, isolated readers, retained unsent records, 15-second roster refresh, protocol compatibility, background known-house entries and names | Real Cloudflare CPU measurement; full journal/gardens/new links still need the website open |
| 4 | Correct navigation and scroll | Person/account landing and character/collection/FC views | Continued real-use feedback on nested Back/Forward |
| 5 | Unified tasks and timers | Character housing, garden care/harvest, events, Fashion and submarine view; shared care rules | Native timer validation |
| 6 | Collection, jobs, character sources | Nine categories, 3,116 entries, filters, pagination, wishlist, jobs; unambiguous hairstyle item unlocks | Every-item scope, full hairstyle availability and exact Lodestone lookup |
| 7 | Automatic catalogue events, Hide/Show | Catalogue entries with progress preserved | Complete quest/access eligibility for every event |
| 8 | One person-name editor | Sidebar rename; style preview read-only | Live usage verification |
| 9 | Plugin sizing and native controls | Minimum size and native window pin/clickthrough/blur support compile | In-game window controls |
| 10 | Character/FC pages and Lodestone | Observed details and FC pages; existing exact links/search retained | Exact automatic Lodestone ID and portrait resolution |
| 11 | Plugin-to-character website links | Character links implemented | Deployed website and signed-in browser test |
| 12 | Icons and approved branding | Approved original artwork, game collection/job icon links | Complete imagery/offline collection icon cache |
| 13 | Selective per-person guest links | Separate revocable capabilities, selected snapshots, moderated messages | Guest photographs and automatic snapshot refresh |
| 14 | Bounded history/cache | 60-day acknowledged-history pruning and acknowledgment cleanup; current/unsent records kept | Long-running configuration growth observation |
| 15 | Text and button audit | Version/setup/observer/garden/picture descriptions revised | Further feedback after live use |
| 16 | Submarine timers | Local and shared observations plus FC/character views | In-game workshop data validation |
| 17 | Automatic quest/event/reward/Fashion | Quest snapshots and repeat transitions; separate possession/unlock state; Fashion 80/100 goals | Every reward mapping; repeatable bits that never reset; native live tests |
| 18 | Collection-linked wishlist | Explicit keys or unique exact names; obtained and learned state | Complete event item mappings |
| 19 | Same-key startup and reconnect | Key normalization, acknowledgments, retry, background refresh, version negotiation | Both clients' live reconnect test |
| 20 | Entry messages and 30-day warning | Green owner messages and daily red reminders from eligible entries | In-game color/threshold test; actual demolition deadline remains game-owned |
| 21 | Automatic character/estate linking | Unique name/home-world match; owned and paired-known-owner house discovery | New unknown characters require person/account selection |
| 22 | Unknown estate names | Placard names and matching FC profile details | Supplied scenario in-game verification |
| 23 | Unnamed cloud pictures | Hash explanation, preview and reference-protecting cleanup | Deployed preview check |

Website delivery remains a manual Cloudflare ZIP. Plugin delivery uses the existing GitHub `main/repo.json` feed. No database, pictures, pairing key or journal password is replaced.

See RELEASE-V7.10.2.md (website) or RELEASE-NOTES.md (plugin) for installation and a focused live test.

Publication history: earlier attempts were blocked by automatic approval review. On 2 October 2026 at 01:43 UTC, the user explicitly requested publication of this prepared source/build and installer feed. Companion 0.5.0.2 is the release target. Journal V7.10.2 remains a separate manual Cloudflare deployment; the full 23-point project remains in progress.
