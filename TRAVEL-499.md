# 0.5.1.499 — Acknowledge successful home return during DC travel

The .498 report records Companion requesting a return from a visited world to the character's home world before continuing to another DC. The game then opens SelectOk with “Successfully returned to Home World.” The previous transfer-message matcher required an explicit world name and a different success phrase, leaving the trip stalled at TransferWait.

Recognize that exact success message only for a committed return-home leg targeting the recorded home world in TransferWait. Acknowledge it once, before lobby lock and DC selection gates, then retain the existing exact-character and current-world verification before advancing. Do not treat dismissal itself as arrival. Continue onward DC travel from character selection through the existing handoff without logging in and back out. The failed-home-return [1000] handling and unknown-message behavior remain intact.

Validation: Release build and gate suite, including phase/ownership/commit guards, wrong home world, rejection/unrelated message cases and existing onward-DC logic. Native UI dismissal and live continuation still need in-game verification.
