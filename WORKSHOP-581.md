# Companion 0.5.1.581

Create, append, clear and edit a character's FC workshop queue anywhere while logged in. New queues are drafts, bound to a workshop only on Start. Existing started queues retain their workshop identity and progress.

Project queue rows have an editable remaining-copy count (Enter to apply) and an X to remove remaining copies. Completed counts and pending deposit receipts are preserved. A final copy awaiting collection cannot be removed. Active routines keep editing disabled. Edits invalidate the material preparation preview.

Regression tests cover draft editing, first-run binding, wrong-workshop rejection, selected-row removal, collection protection, completed counts and pending deposits.

FC chest tab work waits for native StageFlag / IsDataNotReady to clear, the radio tab to be enabled, and the addon to report the selected page before reading stable contents or transferring. Page changes wait for pending inventory operations. Replaces the fixed two-second tab-switch assumption; bounded retries apply only to selecting a tab, never to unconfirmed transfers.

Native field reference: https://github.com/AtmoOmen/FFXIVClientStructs.Expansion/blob/main/FFXIVClientStructs.Expansion/FFXIV/Client/UI/Addon/AddonFreeCompanyChest.cs ; radio controls corroborated by ECommons FreeCompanyChest addon master. Native readiness needs in-game retesting.
