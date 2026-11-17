# Companion 0.5.1.581

Create, append, clear and edit a character's FC workshop queue anywhere while logged in. New queues are drafts, bound to a workshop only on Start. Existing started queues retain their workshop identity and progress.

Project queue rows have an editable remaining-copy count (Enter to apply) and an X to remove remaining copies. Completed counts and pending deposit receipts are preserved. A final copy awaiting collection cannot be removed. Active routines keep editing disabled. Edits invalidate the material preparation preview.

Regression tests cover draft editing, first-run binding, wrong-workshop rejection, selected-row removal, collection protection, completed counts and pending deposits.
