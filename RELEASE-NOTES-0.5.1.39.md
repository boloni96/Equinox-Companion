Equinox Companion 0.5.1.39

- Accept the restricted-access Tend Crop / Quit menu even when its native count still says four. This restores numbered-bed health observations and submitted tend selection matching.
- Observe both native FireCallback and FireCallbackInt entry points. Retain a finalized menu for at most one second to match a late selection callback; consume each selection once. A submitted action still requires its success message or matching harvest item receipt. Inspection, Quit and unrelated loot do not record care or harvesting. Both original native calls are forwarded exactly once without altered arguments.
- Confirmed tending clears stale ready/wilt/dead states, records fresh growing evidence and resets only that bed's 12-hour care timer. Wet soil is independent of maturity. Fresh growing observations supersede expired maturity estimates without inventing a planting time. Only confirmed mature crops use mature artwork and sparkles.
- Planting accounts, groups, characters, houses and batches start collapsed. Expansions remain under the user's control during the session. Blue indicates tending due, green indicates confirmed harvest readiness, orange indicates checks or risk, and red indicates confirmed death.
- FC members without a private house are Floaters regardless of MSQ progress. Private-house owners and FC masters remain Regulars. Shared access does not count as owning a private house.
- Fashion Report shortcuts show a green check when the logged-in character has reached 80 points this week. Local observations and matching shared Journal completion survive restart; other characters and previous weeks do not supply the check. The same check appears on the minimized launcher.

Validation: release build with zero warnings/errors; 640 automated checks. Matching website V7.11.50 tests cover all eight tended beds, independent batches, shared/live care projection, 12-hour wet soil, stale maturity, ownership metadata, Fashion completion, and mobile layout. Native in-game hook dispatch cannot be exercised outside FFXIV; test one tend and one harvest after updating. Reopen affected numbered beds to refresh crop health, and tend again to establish care times missed by the old build. Do not infer historical tend times from inspection alone.

Native callback declarations: https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Component/GUI/AtkUnitBase.cs

Includes the 0.5.1.38 original obtain-chat harvest detection and previous person/account Planting tabs. Website V7.11.50 retains Reset plan from V7.11.49.
