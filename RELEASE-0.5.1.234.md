Equinox Companion 0.5.1.234 — account warning before Lifestream actions

Fixed the title-screen branch that started searching for an unverified paired character before showing the account warning. The same account decision now applies in game, at character selection and at the title screen.

- Verified characters and learned service-account links continue directly, including MAIN and ALT under the same Square Enix login.
- An unverified account shows the warning before any Lifestream search, logout or login. Confirming that it uses the same login permits a bounded exact-character check at the title screen.
- On a fresh game process, saved links alone do not identify which Square Enix login launched the client. A ready character list or local character identifies it. Until then, unverified targets receive the warning.
- Login-check deadlines now run before account observations and other background work. Stop check remains available while a check is active, and the hover shows its remaining time without a contradictory confirmation label.

Regression coverage includes both local service accounts versus both paired accounts, the three game screens, preserved learned links, fresh clients, stale confirmation and an expired busy check. No garden, gameplay-detection, Journal or Worker paths changed.

In-game acceptance: from either local service account, select an unverified paired character at the title screen. The warning should appear before the game changes screens or Lifestream begins a search. Cancel should leave the game untouched. Verified MAIN/ALT switching should still proceed directly.
