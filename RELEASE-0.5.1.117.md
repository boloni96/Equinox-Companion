# Companion 0.5.1.117 — priority treasure-coffer interaction

- Auto-open can act during combat when the character is otherwise available; casting, loading, cutscenes, death, trades and existing conversations still block it.
- Explicitly target the selected unopened coffer for one native interaction, preserving line-of-sight checks, then restore the previous still-valid target unless the game changed it during the call.
- Run coffer observation before other helper interactions, check every 200 ms while auto-open is enabled, and give each click a 250 ms priority window. Movement-stop handling stays active.
- Different nearby coffers can be opened 250 ms apart. An unconfirmed coffer retains the existing bounded retry (three seconds, maximum two attempts per visit); confirmed opened coffers are never retried.
- Applies only to ordinary Treasure objects within 2.5 yalms with the existing coffer marker and auto-open options enabled. No movement, menu confirmation or loot-roll behavior is added.

Validation: transition/policy regression suite and full plugin build run by the publication workflow. Actual in-game timing and interaction remain to be verified; no cutscene crash reproduction was performed.
