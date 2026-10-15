# Companion 0.5.1.139

`/bid` opens a compact Paissa plot finder on the current character's home world. Worlds are grouped in expandable data centres and filtered by region. Search starts on opening, with optional 60-second refresh only while the window is open. District, ward, plot, size, tenant type and phase filters stay above a scrollable table. Click a column heading to toggle ascending/descending sort, including entry count, price, size, phase, updated and first seen. Unknown counts stay unknown.

Each plot offers **GO SEE** through Lifestream's housing-address IPC, plus Copy. Lifestream selects the nearby shard; any further walking follows its own address settings. Companion does not place bids or interact with a placard automatically.

Paired entries appear on matching plots with character, Private/FC and entry number. A separate paired-bids list keeps records visible even when Paissa omits a plot or active filters hide it. Requires Journal V7.11.166 (protocol 20), deployed and opened once. Bid/result observations remain distinct from Paissa's crowdsourced reports. Reminders also consume paired results and suppress claimed bids.

Whereischeap has saved Shopping city and Arrival selectors: Limsa Lominsa, Gridania or Ul'dah, with the main aetheryte or their market-area shard (Hawkers' Alley, Leatherworkers' Guild, Sapphire Avenue Exchange). Travel changes to the listing's world, then continues to that destination. Already being on the world no longer disables Travel. Stop travel cancels the pending continuation.

Validated: local plugin build; existing regression suite; Paissa null/zero-based address/phase/sort tests; shared-bid notification tests; Journal projection tests for identity, privacy and cross-character results. In-game UI and Lifestream routes still need live verification.
