# Companion 0.5.1.85 — recorded NPC conversations

Requires Journal V7.11.84 relay update; update both Companion clients. No Journal completion history is cleared or rewritten by Helper.

The leader records an NPC conversation before the follower opens that NPC. A temporary reservation keeps the follower nearby and holds later travel while the leader finishes. The completed recording is delivered atomically, with captured timing and exact actor/NPC/session binding. Playback waits for the follower's windows to transition and has bounded recording/playback timeouts. Pause/Stop clears reservations. Recordings stay ephemeral.

Fixes the actual JournalAccept layout seen in the supplied .84 reports: quest ID 4779 is at field 261, validated against the displayed quest title and game Quest sheet (legacy 266 fallback is title-validated too). Capture the native Accept submission separately from the game's final accepted flag. The follower closes the offer, plays introductory dialogue, then verifies final acceptance. This avoids waiting for final acceptance before the introductory dialogue that produces it.

Record matching quest Decline and both known Seasonal Event Replay No responses. Cancellation clears that conversation and resumes following, without ending permission. Capture checked/unchecked state for the recognized FFXV replay warning; apply it only to the matching warning before Yes. Arbitrary Yes/No prompts remain outside this handling. First-time/replay differences still require the verified event mapping; different dialogue blocks with a reason.

Build and relay tests cannot prove native game UI behavior. Retest Nananji acceptance, immediate Decline, replay No, checkbox toggling/Yes, and travel after a recorded conversation. Automatic job/gearset changes and leve/duty-specific quest windows remain separate work.
