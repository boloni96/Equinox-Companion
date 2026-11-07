# Exclusive level-one Outfits — 0.5.1.494

Level 1 wearable items now use the Organizer Outfits category (112) instead of their native equipment-slot category. The rule uses equip level, not item level or attribute bonuses. It includes level-one weapons/tools and accessories as well as clothing. Nonwearable items and higher-level equipment keep their existing categories.

The shared category resolver feeds inventory filters, movement rules, assignments and category-based plans. The item catalogue and armoire/dresser category lists use the same level-one mapping. Physical equip slots, game storage eligibility and gear-set protection remain unchanged. Saved Body/Legs/etc. category assignments no longer include level-one pieces; select Outfits for their destination. Explicit item/plan assignments retain their existing precedence.

Validation: build and gate suite; category tests cover exclusivity, higher-level and nonwearable exceptions, and actual routing by Body versus Outfits. In-game UI verification remains pending.
