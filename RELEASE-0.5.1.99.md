# Companion 0.5.1.99

Explicit leader-only Follower will buy the same requests for Shop (gil), ShopExchangeCurrency (including MGP when uniquely identified), and ShopExchangeItem (one to three item costs). Requires Journal V7.11.92 and both clients on .99.

Select the item and quantity in the game's purchase flow, then press the helper button. Observing a purchase never sends it automatically. The helper uses the exact current item/quantity/cost quote, not the leader's row index. It checks the same nearby NPC, local matching row, balances and bag space; submits one purchase; verifies a matching confirmation; confirms success from item and all cost deltas. Pause, stop, movement and character changes cancel pending work. No automatic retries after a submitted purchase.

Limitations: specialized shops (InclusionShop, GC/FC, collectables, cards and services) display an unavailable button; adapters remain to be implemented. Multi-item receive bundles and ambiguous currency icons are rejected. Open a matching vendor category manually if required. Free-slot check is conservative. In-game testing remains required. Cutscene skipping remains disabled. Instance crystal interaction remains separate unfinished work.

Reference review: ECommons Shop/ShopExchangeCurrency addon readers, FFXIVClientStructs AgentShop and AddonSelectYesno, ADS ShopPurchaseRuntime currency and confirmation validation. Implemented independent bounded readers, without copying external helper implementations.
