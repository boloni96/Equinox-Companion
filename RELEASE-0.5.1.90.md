# Companion 0.5.1.90 — Quest scene and Destination recording

Quest Helper now observes native ProcessEventPlay identity and scene instead of relying on EventState1, which was empty in the reported car-event dialogue. EventNpc and EventObj interactions are observed locally; only a verified quest scene, quest offer or existing supported seasonal replay can authorize sharing. Shops remain excluded.

Destination objects use exact object kind, base ID, name, map, world and position. For already accepted quests, the follower must have the same quest sequence. A scene-confirmation step also supports quest interactions without dialogue. The existing right-side approach, in-range fallback and stationary/facing gates are retained.

ESC → Yes is captured from the game-owned cutscene skip dialog with the observed scene. The follower uses its own matching skip callback and reports submitted/confirmed separately. No keyboard ESC or slash injection is added. First-name player references and whitespace are normalized in dialogue.

Both clients must use .90. Journal V7.11.86 remains compatible; no Cloudflare deployment is required. No Journal completion history is changed.

Validation: automated build/test results recorded in the test checklist after CI. Native behavior still needs testing: car-event Destination, Cid intermediate conversation, cutscene skip and vendor exclusion. Different quest sequences and unverified scenes intentionally block replay. Quest rewards and arbitrary Yes/No prompts remain manual. The separate first-time/returning Kipih greeting mismatch remains outstanding.
