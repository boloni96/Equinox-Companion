# Companion 0.5.1.95

- Bring follower back can resolve a public aetheryte on the leader's actual current map when no usable recent teleport exists. The follower selects from their own unlocked, affordable destinations on that exact map. Existing travel permissions, world matching, gil limit and instance checks remain required. Maps without a public teleport still require another route.
- Helper status reports the actual follower acceptance/completion and sequence for the latest verified leader quest received. This uses existing Journal status messages; no website update required. It does not infer acceptance from clicking a button, or discover an unshared quest retroactively.
- A bounded ten-second grace protects active sessions from a brief missing player object around loading transitions. Explicit Stop, character changes and logout handling remain. This hardens a possible transition race; the 18:30 diagnostics do not establish the cause of the lost session.
- Lost leader audiences show a visible notice. Exports now include session settings/status; stops record their reason.
- Automatic cutscene skipping remains disabled. No crash reproduction performed.

Validation: pure regression tests and Windows CI build. Native map data, teleport arrival and quest status still require ordinary in-game validation. Event-vendor purchase requests are a separate planned feature awaiting the target shop/exchange details.
