# Companion 0.5.1.114

- Preserve the last walking direction for up to 1.5 seconds across stationary frames before zone loading. Reject old movement and position jumps.
- Preserve the departure sample when the player temporarily disappears before loading flags; also detect a changed territory between observations. Emit travel only after matching-character, same-world and same-duty arrival validation.
- Record missed boundary-capture conditions for diagnosis.
- Replace misleading approach-cancelled status after a boundary crossing with arrival verification.
- Avoid repeating movement-stop input for every dialogue line and quest-scene confirmation.
- Keep observing a vendor opened by Companion when that exact selected NPC temporarily becomes untargetable during its greeting.

Includes prior .112/.113 corrections. Build and regression checks do not establish in-game success. No Journal deployment or crash reproduction required.
