# Companion 0.5.1.584

Fix stale FC chest quantities in workshop preparation. The screenshots show 8,647 Fire Shards moving between bags and the chest while stored stock remains at an old 517. With a requirement of 22,136 and only those 8,647 available, the shortage should stay 13,489 in either location. The old calculation mixed live bag counts with stale chest data.

A ready, selected chest page now overrides saved records for that page, including authoritative zero/empty contents. Native stage/data locks, pending inventory transactions, selected radio, container loading, matching own-FC identity, and a 750 ms stable sample are required. Unselected loaded containers are not treated as fresh. FC recording no longer requires the separate member-list UI and its character rows to have loaded.

A lightweight open-chest observer samples at most four times a second and saves only settled changed/reopened page snapshots. The latest local page survives closing the chest and is available even with collection sync disabled. Visible workshop material reviews refresh after a settled change while no routine is active. Withdrawal preparation refreshes first and stops on unsettled data rather than using previous allocations.

Regression checks cover stable/transitioning/zero contents, character/FC/page identity changes, and invariant Fire Shard shortages when moving stock. Local gate tests and Release build passed; native in-game reading still needs retesting. Other unopened tabs remain recorded stock until visited.
