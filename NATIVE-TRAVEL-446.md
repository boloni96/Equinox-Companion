# 0.5.1.446 — same-DC estate home return

The estate command's home-return shortcut still used its older logout-only flow. Route same-DC returns through the shared native World Visit engine first, then resume the requested private/FC estate destination after exact-character/home-world arrival. Example: Phoenix → home Raiden stays logged in and uses a city aetheryte.

The continuation is registered only after the native trip starts, avoiding a competing-trip guard. Cancellation stops both the home return and estate continuation. Same-DC travel does not require a known service-account slot. Cross-DC estate returns keep their existing character-selection flow.

Validation: full local build, transition regressions including same/different/unknown DC routing, and release CI. In-game test: start a home-estate command while visiting another Light world; verify World Visit then estate teleport without logout.
