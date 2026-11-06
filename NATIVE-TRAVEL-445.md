# 0.5.1.445 — nearby aetheryte approaches

A nearby city aetheryte could still trigger a 90-second route calculation because the walk goal was beside its pedestal, despite the wider interaction distance. The user supplied a screenshot of the timeout while standing nearby.

Try interacting with a nearby crystal first (within 30 yalms). If its menu does not open, approach the 13-yalm interaction boundary, with the existing closer 8-yalm fallback. The walk destination accounts for the raised crystal origin and walker arrival tolerance; it no longer aims at the pedestal. A smaller-range retry also invalidates the earlier walk goal.

Native travel tries a collision-checked straight path up to eight yalms before using area search. If a blocked native approach needs at most sixteen yalms of movement, fallback search gets five seconds and smaller frame slices (1.5 ms / 48 edges). Longer journeys retain their existing navigation behavior. No unsupported floor or blocked straight corridor is accepted.

Validation includes raised-origin boundary geometry, an inaccessible pedestal with a clear reachable interaction edge, an obstructed corridor, already-in-range behavior and closer fallback. Full local build and independent navigation suite pass; release CI runs all five suites. Live retest: city arrival immediately followed by World Visit/aethernet, checking that the menu opens without an unnecessary walk/search.
