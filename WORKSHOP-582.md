# Companion 0.5.1.582

Workshop Materials and Crafting now send missing raw-material quantities directly to Where Is Cheap. The action refreshes the material review first, updates matching shopping entries rather than adding quantities repeatedly, preserves unrelated entries, and rejects lists above the market tool's limit without partially changing the list.

Click item names to copy them in the workshop queue, workshop materials, crafting materials, and Organizer transfer/list rows. Workshop material headers stay visible while scrolling; quantities use thousands separators. Hover a material to see the quantity used by each parent craft, bag and storage allocations, and recorded storage sources. Crystal demand is for the entire remaining crafting plan and can exceed the per-character carrying limit. Changing the workshop quality filter recalculates the review immediately.

The 0.5.1.578 diagnostic report contains repeated inventory snapshot conflicts. After three conflicting snapshot attempts, independently timestamped locations that were read successfully now update the saved inventory without deleting unread locations or overwriting newer observations. The view remains marked incomplete and the next refresh retries. A complete snapshot still replaces the whole inventory, including removals. HTTP failures and malformed responses keep the existing failure handling. This is partial-progress recovery, not server-side snapshot retention; a full consistent refresh still requires a stable snapshot.

The uploaded report did not include the workshop queue/material calculation needed to reproduce the exact Fire Shard total. New exports include those values and the quantity breakdown. Tests cover shared subcraft demand, stock allocation, yields, direct plus ingredient demand, and crystal requirements above 9,999; no arbitrary cap has been applied to the crafting total.

Retains 0.5.1.581 queue planning outside the workshop, per-row edit/removal controls and native FC chest tab readiness checks.

Validation: local gate/regression suite and Release build passed. The build reports 13 existing warnings in unrelated files. Native clipboard/UI layout, FC chest transitions, and production inventory refresh require in-game verification. The publishing workflow reruns the repository's additional native, market, Keybound and independent-navigation suites before publishing the package and feed.
