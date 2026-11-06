# Companion 0.5.1.454

The screenshot reports Too far away at a house door after the early-arrival change. Remove the unvalidated native CheckInteractRange(type 0) result as authorization to stop/interact. Use conservative three-dimensional physical bounds with line of sight: 3.2 for ordinary interaction targets and 6 for crystal early stopping. These are conservative approach thresholds, not measured maximum game interaction distances.

Remove 30/13-unit aetheryte probes; existing staged menu attempts now begin at 8, then 7 and 6. Hitbox size cannot inflate these bounds. Door standing points reserve arrival tolerance inside the 3.2 bound. The door interaction guard cannot be bypassed by a positive native query. Exports record entrance attempt distance and matching entrance-menu confirmation separately.

Tests cover distant doors/NPCs/crystals, vertical separation, invalid positions, oversized hitboxes and existing approach routing. Release build and independent navigation suite run locally; all five suites run in CI. Actual menu-open boundaries require game observations.
