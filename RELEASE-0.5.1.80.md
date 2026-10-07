# Equinox Companion 0.5.1.80 — Equinox Helper

FollowThem now lives in the Helper tab alongside optional Quest Helper. Existing local settings and follow/travel behavior are retained. Quest Helper and sharing NPC actions start disabled.

- Follower Start grants one session; Stop revokes it. Only the leader pauses/resumes. Leader Stop asks for confirmation and requires the follower to start again.
- The leader gets a FOLLOWED by name top-bar entry and a movable, collapsible Helper Controls window. Quest-enabled sessions open it automatically; closing hides the window without ending the session. Status is temporary and never saved into Journal data.
- Quest Helper records an exact NPC click and matches dialogue text/response choices. Dialogue advancement is observed from accepted Talk-window changes, not mirrored keyboard input. Different quest progress blocks assistance. Rewards, purchases and arbitrary confirmations remain manual.
- A separately permitted cutscene skip follows only the leader's matching event/scene. Uses the current native skip callback and game-owned confirmation. If no callback is available, the game Skip prompt must be opened manually. Unskippable scenes are not bypassed.
- NPC and travel approaches prefer a small offset to the leader's right. Reaching that exact point is unnecessary when the verified target is already within interaction range. Direct approach still uses the existing optional Lifestream integration; no vnavmesh dependency.
- Confirmed missing friend-estate owners cancel that action promptly while preserving FollowThem. An unavailable/empty Friends List gets a bounded four-second refresh opportunity.

Requires Journal V7.11.82 deployed by the owner for new Helper controls, status and quest relay. Existing travel works with V7.11.81. Update both clients for Helper. In-game validation is required; automated tests cannot prove every NPC or cutscene path.

Thanks to TextAdvance/ECommons, YesAlready and FFXIVClientStructs; reference details in HELPER-RESEARCH.md.
