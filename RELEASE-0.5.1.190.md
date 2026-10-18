# Companion 0.5.1.190

Fix native item context menus appearing behind Companion windows. Companion pauses overlay drawing only while a menu opened from its item references is active (including ContextSub). Windows remain open internally, preserving selected tabs, scroll and minimized state. Escape dismisses the game menu without also minimizing Companion. Game observations and background sync continue.

A failed menu open restores drawing within 500 ms. Logout and zone transitions also release the temporary draw pause. No Journal update required. Native menu interaction requires in-game verification.
