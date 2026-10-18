# Companion 0.5.1.194 — remember camera height

- Settings → Camera: optional Remember camera height, saved offset slider, Remember current height and Apply saved height. Disabled by default; preferences remain local to this PC.
- Restores Look At Height Offset only after a zero reset remains stable for 1.5 seconds. Nonzero edits from EasyZoomReborn are left alone; use Remember current height to save a new preference.
- Pauses in loading, cutscenes, dialogue conditions, GPose, PvP, first-person and other active/special camera modes. Character, territory and camera changes restart settling. Disabling stops writes without moving the camera.
- Does not touch zoom, FOV, collision, camera hooks or the game's reset flag. Camera field checks run at most four times per second; safety conditions are checked each frame. No network calls.
- The isolated height field at 0x234 follows EasyZoomReborn's current GameCamera source (https://github.com/FFXIV-CombatReborn/EasyZoomReborn/blob/main/EasyZoomRebornPlugin.cs); normal camera identity/mode comes from FFXIVClientStructs. Recheck this field after game camera-layout updates.
- Regression tests cover settling, interruption, nonzero edits, invalid values, disabled default and persistence. In-game validation of map transitions and cutscenes is still required.

Usage: set the view in EasyZoomReborn, open Settings → Camera, click Remember current height, then enable Remember camera height. To intentionally reset the height to zero, disable restoration first.
