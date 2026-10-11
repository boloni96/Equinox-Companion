# Equinox Companion 0.5.1.119

Adds “Do I already have it?” as a Companion tab, `/doihaveit <name or ID>`, and an inventory item context-menu action. Search covers recorded paired characters and named retainers, with shared FC chest locations kept separate. It shows recorded counts, HQ/NQ and original observation times; older presence-only records retain unknown quantities. Missing results never assert non-ownership. No item movement or purchasing is automated.

Inventory, equipped items, armoury, saddlebags, retainers/market listings and FC chest observations now preserve per-item quantities and quality; crystals are included. Armoire and dresser remain presence-only. Loaded and correctly attributed containers are required. Local newest observations supplement the shared copy. Search results are cached between UI frames.

Workshop readings now include capacity using explored-sector flags for J/10, O/15 and T/20, following AutoRetainer's unlock check. Unobserved capacity remains unknown. Available slots and requirements appear in Submarines; completed timers say “Voyage Complete”.

Deploy Journal V7.11.127 and save once before these new shared observations upload. Other event sync is unchanged. Very large paired search projections are explicitly marked partial; the Journal searches its full saved records.

Validation: official API15 references, Release build with zero warnings/errors, GateTests including item serialization and protocol gating. Journal tests cover authenticated ingestion and live propagation, stale snapshots, empty containers, FC attribution and deduplication. In-game context-menu targeting and native workshop observations still need normal user validation.
