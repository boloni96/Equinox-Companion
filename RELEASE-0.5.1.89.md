# Companion 0.5.1.89 — Quest Helper ignores vendors

Keep Journal V7.11.86; no Cloudflare update.

EventNpc includes vendors, so clicking an NPC alone no longer sends a recording reservation. Capture remains local until a native quest-handler dialogue/choice/skip, verified quest acceptance/decline, or the specifically supported seasonal replay establishes quest context. Only completed recordings with that evidence can be committed and replayed.

Shop windows cancel leader capture; follower playback also refuses to proceed while a shop is open. Unclassified ordinary NPC chatter, shop menus, and legacy standalone NPC commands are ignored. A vendor who also offers a genuine quest remains eligible when the leader actually chooses that quest. Normal FollowThem transport and permitted FATE sync retain their existing paths.

Tests cover vendor/generic menus, named-quest lookalikes, verified acceptance/decline, native quest dialogue and exact seasonal replay. Native vendor and quest behavior still requires in-game testing on both updated clients.
