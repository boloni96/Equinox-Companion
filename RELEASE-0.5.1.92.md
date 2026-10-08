# Companion 0.5.1.92 — Helper status colours

Includes the quest-scene, Destination, cutscene-skip and verified quest-completion changes from .90/.91. Journal V7.11.87 is required for completion replay.

Helper Controls and the single FollowThem top-bar entry use matching status meanings: green working, yellow waiting/loading/paused/stopping, orange blocked, red error, grey stopped or stale/unavailable. Text stays visible and tooltips explain the colours. The leader bar aggregates the highest-severity follower status and lists each follower in its tooltip. Clicking and right-clicking retain their established roles.

A quest blockage remains visible in follower status and leader controls until a fresh quest conversation or session clears it; a successful relay poll no longer hides that failure. Following can continue while the last quest issue is shown.

Native tests remain pending. Optional reward choices and unmatched quest dialogue are still manual. This update does not change Journal completion history.
