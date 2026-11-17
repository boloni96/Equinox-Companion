# Workshop 0.5.1.578

After production turn-ins and a cutscene skip, the game can return to the workshop with no station window open. MenuTransition previously waited for a new menu, blocking the code that would reopen the station. Manual interaction released this deadlock; otherwise it timed out with the stale skipping status.

Track the owned production scene through a stable return to gameplay (500 ms, reset by cutscene/loading). Release Escape input when the scene ends, clear the completed scene and previous menu, and resume Browse so the station is reopened automatically. Keep project identity, delivery counters and collection receipts intact. Only a scene actually observed during the armed production transition can release this wait; an absent menu before a scene starts cannot.

Regression checks cover absent menus, natural or skipped completion, loading, transient condition changes and reset between projects. In-game full-project confirmation remains necessary.
