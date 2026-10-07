# Equinox Helper references

Inspected 2026-10-07. Equinox's code remains self-contained; TextAdvance, YesAlready and ECommons are not new dependencies.

- TextAdvance, commit 9dee62760472b08a7c36c596c64e4dfbfdc5cf8b: Executors/ExecSkipTalk.cs and Executors/ExecConfirmCutsceneSkip.cs separate subtitle advancement from cutscene confirmation. https://github.com/NightmareXIV/TextAdvance
- ECommons, commit 9ef3961c329fa99bd4c65769b39a56cbe2d2917a: Automation/AutoCutsceneSkipper.cs checks for a skippable callback and triggers the native cutscene input path. Equinox does not copy its instruction patch; it calls the exposed AgentCutscene.OpenSkipDialog only with the current non-null SkipCallback. https://github.com/NightmareXIV/ECommons
- YesAlready Features/Talk.cs uses the Talk addon to advance dialogue. https://github.com/PunishXIV/YesAlready
- Authoritative structures: aers/FFXIVClientStructs AgentCutscene (SkipCallback, SkipDialogAddonId, OpenSkipDialog, TalkName, TalkText), EventFramework (EventState1, Scene), AddonSelectString, AddonSelectIconString, AddonCutSceneSelectString, InfoProxyInterface.RequestData. https://github.com/aers/FFXIVClientStructs

Equinox adds its own session permission, exact NPC and dialogue matching, expiry and source-area checks. A leader's skip is replayed only for the same event/scene. Normally unskippable scenes remain unskippable. An unavailable callback leaves the action waiting for the game's Skip prompt; it never sends global Esc input.

Dialogue capture watches the Talk window's accepted text changes/closure, independently of whether the leader used mouse, keyboard, controller or another dialogue plugin. The recipient uses the existing bounded Talk event path. Distinct text may differ with quest progress or localization and deliberately blocks replay.

## Quest acceptance (.82)
TextAdvance ExecQuestAccept uses JournalAccept button 44; ReaderJournalAccept reads quest ID from AtkValue 266. FFXIVClientStructs QuestManager.IsQuestAccepted confirms the leader accepted and the follower completed acceptance. ECommons ClickHelper documents dispatch through the button registered event. Equinox watches offers without accepting them on the leader, sends only confirmed acceptance, then verifies exact quest and NPC conversation on the follower. No ECommons dependency or input injection.

## Event replay and FATE sync (.82)
User screenshot confirms Kipih Jakkya's English SelectYesno prompt: Do you wish to replay the event? Official event page https://eu.finalfantasyxiv.com/lodestone/special/ffxv/8ghatq7szp/ confirms completion remains set during seasonal replay. Equinox preserves Journal event history and maps this specific leader Yes to the same replay confirmation or first-time The Man in Black offer. No arbitrary Yes/No mirroring.
FFXIVClientStructs FateManager exposes SyncedFateId, CurrentFate, IsInFateRadius, IsSyncedToFate and LevelSync; FateContext exposes FateId, StartTimeEpoch and running state. Equinox observes new confirmed sync, bounds it to a permitted Helper session and same live FATE, and uses existing direct Lifestream movement. No OS keyboard injection or combat logic.

## AutoDuty study — 7 October 2026
Active project: https://github.com/erdelf/AutoDuty (the ffxivcode repository redirects development there).
Reviewed AddonHelper.cs, ActiveHelpers/QueueHelper.cs, Managers/ActionsManager.cs, PlayerHelper.cs and README.

- AddonHelper tracks a ready window being seen, throttles Talk/Yes-No/result clicks, then requires that window to disappear or stop being ready before reporting completion. Relevant design lesson: a submitted click is not a confirmed transition. Equinox must additionally retain exact quest/NPC/session matching.
- QueueHelper checks player readiness, existing duty queue state and ContentsFinderConfirm separately. Duty Support/Trust registration uses separate agents, not a universal quest-accept callback. Useful for later quest-duty entry work.
- ActionsManager has separate Talk, response, result and object-interaction steps. Index-driven recorded routes are not sufficient for shared quest menus with different options; Equinox retains text and quest identity matching.
- PlayerHelper distinguishes valid, occupied and animation-locked states. Preserve bounded waits and explicit blocked reasons rather than treating every waiting state alike.
- AutoDuty's dungeon movement relies on vnavmesh and combat integrations. No AutoDuty dependency, navigation requirement or combat automation has been added to Companion by this study.
- The reviewed code is unlicensed/default copyright. Notes describe behavior and public game interfaces; no AutoDuty implementation was copied.

Still separate work: job/gearset mirroring requires exact ClassJob matching against the follower's own usable saved gearsets; levequest windows and quest-specific duties need their own verified state transitions. These are not claimed implemented in .84.
