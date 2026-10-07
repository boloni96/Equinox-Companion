# Companion 0.5.1.68 — FollowThem travel recovery

Failed door interactions could block later independent travel and then discard the entire queue. Party teleport arrival could also be followed by a duplicate self-cast.

- Preserve a fresh independent World/DC request, or a remote Teleport/estate request when Meet at Teleports is enabled, after a failed trip. Discard dependent steps that cannot be executed from the current location; retain the following route once independent travel resumes.
- Reconcile known arrival coordinates, world, territory, map and instance before casting a shared teleport. Allow a pending accepted party offer time to finish and suppress the late duplicate at its destination.
- Face and interact with only the recorded nearby object, using direct interaction without the camera line-of-sight gate, as used by Lifestream. Keep target identity, source position, range, stationary and bounded retry checks. No camera keys or target cycling.
- Select a uniquely matched numbered instance from a visible ready menu even over the loading screen. Abort only Companion-owned approach movement first. Preserve pairing/session/expiry checks.
- Keep World/DC requests pending through temporary unavailability and permit dispatch from a different territory on the same source World. A queued trip pauses ordinary follow rather than silently returning to run-follow.

Journal remains V7.11.80; no new website deployment for this release. No new Journal data or movement uploads. Existing Stop/key acknowledgement and pickup implementation remain unchanged.

Validation: Release build succeeded with 0 warnings/errors. 1,179 automated checks passed, including new duplicate-arrival and dependent/independent queue regression cases. Native FFXIV interactions require in-game acceptance; automated checks do not establish live success.

User confirmed on .67: a clean Start session successfully relayed `/equinox travel Siren`; Stop and nearby pickup had worked. Reported .67 failures include duplicate party teleport, off-screen door interaction, instance choice during loading, and failed-door queue blocking later travel. Retest these on both updated clients.

World shorthand and a shorter command remain deferred. Use `/equinox travel Siren` for the controlled World/DC test.

References: https://github.com/NightmareXIV/Lifestream and https://github.com/NightmareXIV/ECommons (exact-object direct interaction); https://github.com/aers/FFXIVClientStructs (native APIs). No vnavmesh dependency added.
