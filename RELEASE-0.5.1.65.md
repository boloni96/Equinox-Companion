# Companion 0.5.1.65 — movement input and retained door travel

Update both clients. Journal stays V7.11.79; no Cloudflare deployment.

Evidence from .64
- Both supplied follower exports contain 23 movement-stop observations; all report inputReads=0. The GetInputStatus hook did not intercept the intended backward input.
- User confirmed Ul'dah crystal -> shard -> crystal and FC estate travel, but reported unwanted jumps/target selection and target-cycling errors.
- Exports confirm private/FC estate arrivals, failed stop requests, three unanswered Entrance interactions, and door rejection clearing dependent trips.

Additional .64 export at 21:41 confirms World Visit arrivals at Marilith and Rafflesia, FC teleport, and house entry; additional-chambers interaction still timed out. No claim that the stop hook succeeded.

Changes
- Remove the GetInputStatus hook and all calls to that native input-query path. Manual movement now reads stored UI-filtered key states and configured movement bindings; gamepad left-stick movement is also observed.
- Stop uses the configured unmodified backward-movement key for 100 ms through messages addressed only to the current process's FFXIVGAME window. No global SendInput, foreground switch, default guessed key, target-cycle key or jump. A modified/missing binding, text entry, or unavailable window leaves the stop unconfirmed with a manual-stop message. Release is requested on completion, character change and disposal.
- Game follow cancellation must be observed before stop is considered confirmed; stationary position alone is insufficient. Top-bar Stop reports STOPPING while unconfirmed. Travel approach and interaction wait for cancellation.
- Vertical-only jumping no longer resets the stuck timer. Horizontal movement and the existing pickup/resume rules are retained.
- Keep the queued head trip when its source cannot yet be used, retrying once per second with the original expiry. Dependent trips are cleared only if the head expires. Failed door dispatch remains in preparation instead of falsely waiting for arrival.
- Clamp transport approach points to the source's interaction edge, require matching source object kind, and explicitly select the identified source before interaction. Interaction remains limited to three attempts; no unrelated objects or unknown menu choices are clicked.
- Check party teleport offers even while the arrival queue is active, keeping armed-session, option, prompt and movement gates. Do not report Lifestream failure during loading.
- Preserve the .64 aethernet header correction, room-number/owner matching, estate row selection and same-ward departure detection.

Validation
- Release build: zero warnings and errors. 1,130 regression checks passed.
- Added movement-key filtering and a jumping-at-wall 10-second timeout/pickup test.
- Game-window delivery, native cancellation, absence of target-cycling errors and door replay still require in-game testing. These tests cannot prove FFXIV runtime behavior.

Ordered in-game retest
1. Update both clients to .65. Start FollowThem in open space; click top-bar Stop while the leader keeps walking. Follower must actually stop, then display STOPPED. A brief backward step is expected. If STOPPING persists or movement continues, export follower diagnostics and stop testing travel.
2. Restart; stuck timeout 10 seconds. Against a wall, jumping alone must not keep resetting the timeout. Follower must cancel movement and wait. Bring leader closer and verify configured Resume nearby.
3. Leave FollowThem active briefly without touching movement: no random target cycling, focus targets, jumps or fake 'Your movement' pauses.
4. Ul'dah crystal -> shard -> crystal, then private estate and FC estate. Check actual arrival and no repeated red target-cycling errors.
5. House -> workshop -> house -> outside; then numbered/private chamber. Verify the exact door is selected. Try moving briefly during preparation with Stop on movement OFF; release should resume the retained trip. Original two-minute expiry is not extended.
6. With two game clients, verify Stop only affects the follower client. Open chat while a stop is requested: it must wait rather than type a movement key into chat.

Remaining limitations
- Unmodified keyboard movement binding is required for automatic stop; otherwise use a real movement key and export diagnostics.
- No obstacle navigation added. Room selection on another page remains manual. Flight landing/dismount edge cases are unchanged.
