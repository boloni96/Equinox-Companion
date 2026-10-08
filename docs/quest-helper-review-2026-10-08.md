# Quest Helper review — 8 October 2026

## Evidence and current boundary

The supplied crash stack contains Companion UpdateQuestHelper, FireCallbackInt and AgentCutscene.ReceiveEvent followed by an access violation. It identifies the skip/confirmation path, but does not prove a stale callback was the precise cause. Automatic cutscene opening and confirmation stay disabled. Do not reproduce the crash.

Both 18:30 exports run 0.5.1.94. The client that observed Mitainie's dialogue at 18:30:12 had already logged no active followers at 18:30:01. No corresponding conversation recording is present. The old exports lack session-stop reasons and current Helper settings, so they cannot distinguish a user stop, relay expiry or transition race. The game message about no longer following only describes native movement; it does not alone prove Helper permission ended.

## Source comparison

Reviewed pinned upstream sources:

- [YesAlready](https://github.com/PunishXIV/YesAlready/tree/5b36a156e0d9d84b0969208c38f441929e0d2c79): readiness checks, configured prompt/target/zone matching, enabled Accept/Complete buttons, Talk.Progress. Request hand-ins use NPC trade state. It is not evidence that any arbitrary YES is safe to click. Its temporary preference toggles can overwrite user intent; scoped ownership is preferable.
- [TextAdvance](https://github.com/NightmareXIV/TextAdvance/tree/9dee62760472b08a7c36c596c64e4dfbfdc5cf8b): separate cutscene-open and prompt-confirm stages, localized text matching, ready addon checks, named external-control ownership. Nullable external settings inherit user preferences, so unrelated automation must explicitly be disabled in a scoped integration. Reward selection includes fallback behavior Companion must not copy blindly.
- [ECommons AutoCutsceneSkipper](https://github.com/NightmareXIV/ECommons/blob/9ef3961c329fa99bd4c65769b39a56cbe2d2917a/ECommons/Automation/AutoCutsceneSkipper.cs): intercepts the game's cutscene input routine and temporarily patches its input condition. This is native hooking, not a harmless simulated Escape key, and is not automatically safe for Companion's SDK/game version.
- [Questionable](https://github.com/PunishXIV/Questionable/tree/78d512148de89384bb2d135ba62ee006ba80fa83): real accepted/completed/sequence checks, explicit wait tasks, dialogue matching and TextAdvance ownership integration. Aetheryte coordinates can be reconstructed from the correct map's marker rows; region-map duplicates must not replace actual-map identity.
- [Lifestream](https://github.com/NightmareXIV/Lifestream/tree/ef759e9d3c3cd989b4af569c3f770ee9ba060922): unlocked teleport lists, availability checks and observed arrival. Multi-stage town/aethernet routing is separate from choosing a public teleport in a zone.

## Upgrade decisions

1. Implement actual follower quest state, clear session-loss diagnostics and current-map public-aetheryte meeting fallback in .95. Reuse existing Journal status transport.
2. Keep cutscene skipping disabled. Investigate a scoped optional TextAdvance integration with matching quest/scene, explicit false settings for unrelated features, single-owner acquisition, and cleanup on pause/stop/logout/unload. No implementation or safety claim yet.
3. Quest hand-ins need exact item, count and quality verification. Optional rewards need the follower's explicit policy; never choose randomly.
4. Event vendor copying needs an explicit leader request and follower confirmation for exact item, quantity, currency and cost. Restrict support to identified event exchanges. Recheck stock, balance, inventory capacity and the current shop before submission, and confirm the purchase from resulting state. No general vendor automation or purchases from conversation replay. The target shop must be supplied before implementing its native exchange path.
5. Continue distinguishing recording, delivered actions, submitted UI input and actual game completion. Do not treat cloud uploads or a button click as proof of game progress.
