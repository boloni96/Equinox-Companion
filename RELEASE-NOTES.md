# Companion 0.5.1.112

- Omit contiguous leftover dialogue from the exact scene just skipped, including Talk snapshots finalized after YES. Preserve dialogue across other scenes, NPCs, quests and intervening choices.
- Keep recording after solo-duty Proceed so the following dialogue and cutscene skips are included. Commit at the loading boundary or normal conversation end; Leave still commits immediately.
- Preserve the recording owner's identity when committing during loading.
- Wait for a visible purchase confirmation to become ready and contain text. Exact nonempty prompt mismatches still stop the purchase and now log expected and actual text.
- Native cutscene input and YES implementation are unchanged.

Regression/build checks do not establish in-game success. No crash reproduction or Journal deployment required.
