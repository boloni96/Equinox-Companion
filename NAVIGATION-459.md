# Companion 0.5.1.459

Housing routes can now prefer roads identified in the game's own map textures, aligned using the current map's size and offsets. Assets load in the background and are cached; opening the map/minimap is unnecessary. This is a soft route cost, not proof of walkability. Ground support, body collision and live geometry remain authoritative. Unavailable or unrecognized artwork falls back to geometry. Map palettes and subdivision alignment need in-game validation.

Walking prefers approximately three yalms of clearance where space permits. Narrow passages remain usable. Steering now applies clearance checks to shortcuts and fallback targets; a blocked connector stops movement and requests replanning instead of sending forward input. Obstacle replanning is capped at 15 seconds; initial path searches retain their existing timeout.

Garden plot entry, mapped-bed approaches and event garden return entry now use Companion's native navigator and game housing entrance positions. Cancellation, character/world/territory changes and missing targets stop the movement. Existing garden interaction and confirmation logic remains in place. Missing entrance data stops with an explanatory diagnostic rather than falling back to Lifestream.

Exports record map asset selection, road coverage/fallback and garden movement starts/stops. Build and regression suites cover map alignment, road preference, invalid image fallback, blocked steering, clearance and obstacle costs. Live tests are still required for Goblet corner recovery, Shirogane paths, plot entry, tending and cancellation.
