# Companion 0.5.1.108

- Replace Companion's direct cutscene confirmation callback and window-message Escape path with TextAdvance external-control IPC, following the integration approach used by Questionable.
- Requires TextAdvance on the follower. Enable Companion's cutscene-mirroring option, then start a new follower session.
- Request the skip prompt only for the leader's matching recorded scene. Enable TextAdvance confirmation only after the live game-owned prompt and recorded choices match.
- Release control on scene exit/change, pause/stop, permission loss or timeout. Refuse to take control from another plugin.
- Disable TextAdvance dialogue, quest acceptance/completion, rewards, hand-ins and auto-interaction during the scoped request.
- Build/regression validation does not prove in-game stability. No crash reproduction requested. Post-Proceed continuation capture remains separate.

References: PunishXIV/Questionable TextAdvanceIpc; NightmareXIV/TextAdvance IPCProvider, Config and ExecConfirmCutsceneSkip. AutoDuty reviewed for cutscene handling; no AutoDuty code copied. No Journal deployment needed.
