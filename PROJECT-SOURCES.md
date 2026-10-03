# Equinox Empire — source index

Updated 2026-10-03. This index preserves verified references recovered from project notes and conversation history. It is not a claim that every link from every earlier chat has been recovered. Add future references here with their purpose, review date, findings, and implementation status.

External project names belong in research and future credits. User-facing Companion and Journal features should use Equinox's own names. Preserve required attribution and license notices separately.

| Source | Why it matters to Equinox | Review/status |
| --- | --- | --- |
| [Altoholic](https://github.com/Sohtoren/Altoholic) | Character details, collections, housing observations, currencies and allowances | Source reviewed; see ALTOHOLIC-RESEARCH.md. House permission is not ownership. Website collections and guest visibility remain distinct product requirements. |
| [AutoRetainer](https://github.com/PunishXIV/AutoRetainer) and [AutoRetainerAPI](https://github.com/PunishXIV/AutoRetainerAPI) | Cached submarine data across characters, parts, return times, carried ceruleum tanks and repair kits | Reviewed; optional read-only cache integration implemented in Companion 0.5.1.15. Live validation still needed. Cache import time is not game observation time. See AUTORETAINER-RESEARCH.md. |
| [SubmarineTracker](https://github.com/Infiziert90/SubmarineTracker) | Submarine builds, routes, voyage times, repair/return notifications and loot history | Registered in Journal V7.10.0 references; README rechecked 2026-10-02. No new implementation claim from this review. |
| [Henchman](https://github.com/Knightmore/Henchman) | TestyTrader transfer goals, partner validation and multi-character trade coordination | Source reviewed 2026-10-02; see HENCHMAN-RESEARCH.md. Research only; no Equinox gil-transfer feature implemented. |
| [PlayerTrack](https://github.com/Infiziert90/PlayerTrack) | Character identification and Lodestone profile links | User-supplied reference recovered from project history. Further focused source review required before adopting an approach. |
| [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) | Native housing identity, planting agents and game-state structures | Existing technical source. Relevant files include HousingManager.cs, Housing/OutdoorTerritory.cs and UI/Agent/AgentHousingPlant.cs. Revalidate against the target game/API version. |
| [FFXIVGachaSpreadsheet](https://github.com/Infiziert90/FFXIVGachaSpreadsheet) | Collection/reward reference | Preserved from Journal V7.10.0 sources; no new source audit in this pass. |
| [FFXIV Collect](https://ffxivcollect.com/) | Collection reference and possible website presentation ideas | Preserved from Journal V7.10.0 sources; verify data/API availability before integration. |
| [Lotlab FFXIV Gardening Tracker](https://github.com/Lotlab/FFXIV-Gardening-Tracker) | Existing-crop seed identification, garden event/state research | Reviewed 2026-10-03; see GARDENING-TRACKER-RESEARCH.md. ACT/CN/GPL3 reference; no packet offsets or code imported. State bytes are not decoded into artwork states. |
| [FFXIV Gardening](https://www.ffxivgardening.com/) | Crop growth/wilt data, crossbreeding, planting order, soil and fertilizer | Existing gardening research; specific pages below. Timers are estimates unless confirmed by game observations. |
| [Fashion Report image](https://fashionreportxiv.com/hint.png) | Weekly Fashion Report guide | Existing project reference. Check the weekly report date before treating an image as current. |

## Gardening references

- [Seed list](https://www.ffxivgardening.com/seed-list.php)
- [Gardening guide](https://www.ffxivgardening.com/guide-to-gardening)
- [Intercross priority path](https://www.ffxivgardening.com/intercross-priority-path)
- [Fertilizer analysis](https://www.ffxivgardening.com/fertilizer-analysis)
- [Tips and tricks](https://www.ffxivgardening.com/gardening-tips-tricks)
- [Documentation](https://www.ffxivgardening.com/documentation)

Keep crop-specific maturity and neglect estimates separate. Tending and fertilizer are different events. A plan must not invent a planted/tended/harvested observation. Use verified actor pairing for attribution, verified house/world/patch/bed identity for linking, and retain unknown values when evidence is missing.

## Equinox's own references

- [Companion repository](https://github.com/boloni96/Equinox-Companion)
- [Equinox Journal](https://equinoxjournal.pages.dev/)
- Local garden handoff provenance: `garden-zip-review/unpacked/Equinox-Garden-Handoff-v1.0.0/docs/08-SOURCES-AND-PROVENANCE.md`.
- Existing project research: `ALTOHOLIC-RESEARCH.md`, `AUTORETAINER-RESEARCH.md`, `HENCHMAN-RESEARCH.md`; Journal release notes retain earlier source lists.

## Ideas to revisit with the user

1. Gil transfer goals between explicitly selected paired characters: fixed amount, reserve, or target balance; keep a confirmed transfer ledger.
2. FC supply overview: carried tanks/kits and submarine needs, with source age and character/account ownership clear.
3. Website collections with explicit guest visibility controls, separating unlocked collections from inventory possession.
4. Character identity reconciliation across person/account/home world and visiting world, with unresolved evidence visible rather than guessed.
5. Credits page grouping original projects, data references and artwork provenance without putting third-party branding into everyday feature labels.

These are proposals, not scheduled work or promises that the underlying game data is always available. Future source studies should record an exact commit where possible and distinguish documentation review from implementation and live testing.
