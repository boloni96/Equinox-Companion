# Companion 0.5.1.94

Fix the quest turn-in wait shown in the 2026-10-08 18:13 diagnostics. The game displays four informational Talk pages after Complete for The Man in Black, while completion is still pending. Previously the helper waited for completion before processing those pages and timed out after 20 seconds.

After the result window closes, play only the immediately following recorded Talk pages from the same quest and conversation, then verify the game's completion state before the next non-Talk action or conversation. Submitting Complete or closing its window is not treated as successful completion.

Quest dialogue uses conversation, native scene, speaker and text identity instead of requiring the NPC to remain selected. Travel keeps its target checks. Dialogue clicks do not run underneath another choice, acceptance, or result window. Unavailable clicks now have a bounded wait and a diagnostic reason.

Automatic cutscene skipping remains disabled. The earlier crash log confirms a null-address access violation in AgentCutscene.ReceiveEvent reached from Quest Helper's FireCallbackInt. It does not establish that switching callback APIs would fix the underlying callback lifetime problem. No native skip/YES crash reproduction was attempted. This release fixes quest dialogue progression, not automatic cutscene skipping.

Regression tests cover the four reported information pages, unrelated scenes/conversations/leaders, missing signatures, decline, and choice/new-quest boundaries. Windows CI must pass all tests and build before installer publication. Native in-game progression remains to be verified.
