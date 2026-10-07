# Companion 0.5.1.84

Includes .83 visible Talk capture/recovery fix. When a dialogue action comes from a game-confirmed active quest, it now carries that quest identity. If the follower still has that exact quest offer open, acceptance is handled before the pending dialogue action; the dialogue remains queued until acceptance is confirmed. Different offers are never accepted. Missing identity reports the acceptance prerequisite explicitly. Selecting a quest name alone is not acceptance permission.

Being followed and being a follower are now exclusive. The leader top bar keeps its Pause/Resume role through stale status and temporary relay errors; a stale follower handler also cannot start a local follower session. If followers arrive while you were following someone, your own outgoing follow session stops. Ending the incoming sessions releases the leader role.

Journal remains V7.11.83. Update both clients; retest Nananji starting from the follower's quest offer, plus the top-bar role during follower loading. No changes to Journal completion history.

Acceptance dispatch now requires a ready JournalAccept window and the native Accept button's registered click event, instead of assuming the first event is a click. Quest IDs are normalized to sheet row IDs. Acceptance and attempts are recorded separately, and dialogue cannot proceed while the offer remains open. This directly addresses the user's clarification that both had the same quest and follower acceptance did not complete.

Requested broader coverage is tracked separately: automatic job/gearset switching, levequest-specific windows and quest-duty initiation are not implemented by this release's standard acceptance fix.
