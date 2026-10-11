# Companion 0.5.1.129

Where Is Cheap now imports Artisan's **Export Remaining Ingredients as Plain Text** output. Choose **Import Artisan list**, paste lines such as `3x Ashen Alumen` and `180x Megalania Skin`, then import. Quantities and names are saved; repeated names are combined. Import replaces the previous list.

Each item starts collapsed, with its quantity, known source categories and best complete world estimate. Expand it to edit the quantity, inspect sources, compare worlds and stacks, or ask Lifestream to travel. Region, world and quality filters apply to the entire list. Unknown or ambiguous English names are reported; nonmarketable items retain their source information.

Sources come from local game sheets: gil and special-currency shops, Grand Company shops, recipes, gathering areas, fishing spots and retainer ventures. Source details include known areas or shop names. **Show vendor locations** searches the optional Item Vendor Location plugin for NPC locations, using its supported `/pvendor` command. This index is not exhaustive: monster drops, hidden nodes, rewards and some exchanges may be missing, and unlocks still apply.

Whole-stack estimates select cheapest unit-price listings until the requested quantity is covered, or a cheaper single sufficient stack. These are estimates, not an exhaustive minimum-cost optimization. Returned stock is limited to 100 listings per queried world/region; taxes and sold/stale listings can change the purchase cost. Partial-stock worlds are marked and rank below complete plans.

Price checks run one item at a time while the window is visible, with 15/30/60-second per-item refresh and retry backoff. Large lists take longer to cycle. Source lookup is cached and incrementally prepared on the game thread. Closing/minimizing the window starts no further checks; an existing request may finish.

Validation: release build and regression tests. In-game validation of window layout, live source data and optional vendor/travel plugins remains necessary.
