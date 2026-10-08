# Companion 0.5.1.106

- Resume an already-open dialogue only when a recent local NPC click, exact NPC identity/location, current native quest scene, quest step, and the recording's first dialogue line/signature all match.
- Fix the observed race where the follower clicked Noctis as the completed recording arrived, causing the recording to be discarded as an unrelated conversation.
- Keep unrelated, stale, advanced, or unverified windows blocked. Never adopt a choice, purchase, duty prompt, or cutscene skip through this path.
- Add regression coverage for matching dialogue and identity/scene/session mismatches.

Questionable reference reviewed: PunishXIV/Questionable, In the Dark of Night (3159) quest path and interaction state handling. Its per-objective quest variables are useful for future sub-objective reconciliation; no quest database or autonomous routing was imported in this release.

Build/test success is not in-game verification. Cutscene skipping remains unresolved and should stay disabled. No Journal deployment required.
