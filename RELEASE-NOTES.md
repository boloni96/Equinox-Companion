# Companion 0.5.1.110

- Add a built-in Companion cutscene-skip provider with no TextAdvance dependency.
- Keep TextAdvance as an optional provider; provider changes apply at the next follower session.
- Built-in requests InputId.ESC for a bounded 100 ms pulse only in the exact recorded quest scene, with active skip permission and no blocking menu/text entry.
- Select Yes through the verified game-owned skip list's ListItemClick event once. No direct cutscene agent call, OpenSkipDialog, FireCallbackInt, or constructed confirmation callback.
- Stop input on completion, timeout, pause/stop and disposal. Never automatically switch providers after failure.
- Regression coverage checks input identity, scene/permission boundaries, pulse expiry and defaults.

Skipping remains opt-in. Build/tests do not establish in-game operation or crash-free behavior. Post-Proceed continuation recording remains unsupported. No crash reproduction requested and no Journal deployment needed.

References: FFXIVClientStructs InputData/InputId, AddonSelectString, PopupMenu and AtkComponentList APIs; Questionable/TextAdvance integration remains available. Built-in implementation does not copy ECommons' cutscene code patch.
