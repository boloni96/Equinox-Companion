# Companion 0.5.1.86

Requires the existing Journal V7.11.84. No new Cloudflare package.

Fix orphan dialogue emission after a paused/cancelled recording. Only an active new recording can capture and send NPC steps. Completed conversations are committed before a subsequent genuine NPC click, even inside the quiet debounce period. Up to four completed conversations can wait behind current playback, preserving replay setup before quest acceptance.

Normalize wrapped seasonal-replay warning text and inspect visible SelectYesno instances 1–4, retaining exact event/NPC/prompt matching. This addresses missed confirmation recognition without copying arbitrary Yes/No responses. Native replay checkbox behavior and rapid reinteraction still require in-game verification.

An unchanged exact dialogue line gets at most three clicks, at least two seconds apart; every attempt checks the line again. Failed owned conversations are cleared promptly, including dependent queued recordings, rather than holding the player for the ten-minute recording lifetime. Follow session permission remains active. Pause/Stop clears pending conversations.

Previously reported passes: ordinary quest accept/decline, replay No/checkbox case, and conversation followed immediately by teleport. Latest diagnostics showed one later Nananji success, plus orphan dialogue and a stalled line; those failures drive this correction. No Journal event-completion history reset, job switching, or new duty/leve automation.
