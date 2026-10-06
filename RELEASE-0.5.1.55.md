# Companion 0.5.1.55

FollowThem now reports STARTED, STOPPED, waiting, follow requests, supported
portal/teleport actions and errors in chat. Chat messages can be disabled in the
FollowThem tab. Repeated unchanged status and repeated relay failures are quiet.
The top-bar wording and saved target/settings are preserved.

Movement commands wait through loading, occupied events, casting and cutscenes,
then require 750 ms of stable playable state. Stop does not send a movement
command while the game is unavailable. A recently issued movement command rejected
by the game's English error message stops the local session and explains how to
retry; the original game error is not hidden. Duty entry still needs live testing.

Coffer minimap markers now tint the single native chest icon instead of drawing
an outline over it. Original colours are restored when markers are disabled or
nodes are reused. No cached native pointer is dereferenced after an addon rebuild.

Separate minimap and main-map toggles are under Settings > Features > Treasure
Coffer markers. Main-map markers use the same visit observations and opened state,
only on the matching territory/map floor. They use the map texture's current
bounds for pan/zoom and are clipped to the map component. No new chest scans or
Journal sync are introduced by drawing either map. Main-map alignment, clipping,
colours and native minimap tinting require in-game acceptance testing.

Existing feature enable settings remain unchanged. New installations still have
FollowThem and coffer markers disabled. Both map-display options default on inside
the optional coffer feature. No automatic coffer interaction.

Journal stays V7.11.74. This release needs no new Cloudflare package; V7.11.74
remains required for the optional portal relay.

Validation: 941 automated checks; Release build with zero warnings/errors.
No in-game client is available in the build environment. Automated policy and
projection checks do not establish native drawing alignment or FPS performance.
