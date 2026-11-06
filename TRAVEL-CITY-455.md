# Companion 0.5.1.455

Settings > Travel exposes an ordinary Travel city preference (Automatic, Limsa Lominsa, Gridania, Ul’dah), defaulting to Gridania and stored separately from ShoppingCity/ShoppingArrival (Limsa Lominsa by default). Existing explicit selections are preserved. The same page also exposes the existing shopping controls. Ordinary native journeys capture the selected travel gateway when starting; shopping-list journeys capture the shopping city instead.

When a World Visit city teleport is necessary, prefer the selected unlocked main aetheryte, falling back to an available main city if locked. Automatic preserves previous first-available behavior. Nearby usable city crystals are reused, and direct estate teleports are not rerouted through a city. Cross-DC lobby travel does not gain an unnecessary city leg. Exports record journey owner, preferred gateway, selected gateway and fallback.

Regression cases verify independent ordinary/shopping settings, preference ordering, locked-city fallback, Automatic and no unlocked main city. Build and gate suite locally; all suites in release CI.
