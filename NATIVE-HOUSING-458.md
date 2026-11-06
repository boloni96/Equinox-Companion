# 0.5.1.458 — native GO SEE housing address route

GO SEE keeps direct estate/friend teleports and replaces its Lifestream housing-address fallback with Companion stages: verified game layout, destination world, district city, ward selection and confirmation, residential aethernet, collision-checked walk to the plot's placard approach. Supports the five mapped housing districts, wards 1–30, plots 1–60, including subdivisions. Regional preflight from 457 remains in force.

Positions are read from HousingLandSet placard instance IDs and HousingAethernet instance IDs in the installed game's layer files. File parsing runs on a background worker and caches by district. Missing or invalid data stops before travel and records missing IDs rather than guessing. Shard choice is the nearest recorded position within the plot's division; this is not an optimal road-distance guarantee. Actual movement uses the existing collision and previously walked route system. Map/minimap artwork is not interpreted as a walkability map.

Goddess indicator shows housing progress and right-click cancels movement and native world continuation. Gardening/delivery now wait for and cancel native housing travel correctly. This change does not remove unrelated Lifestream integrations, including existing garden-specific plot-entry/mapped-bed movement.

English ward confirmations require both the requested ward number and district. Unrecognized/localized prompts wait for manual confirmation. Arrival must confirm the actual world, territory and ward; subdivision shard coordinates are checked before the final walk. Context changes and stage timeouts stop the trip.

Validation: release build and housing policy tests for district/address bounds, invalid geometry and wrong ward/district confirmation rejection. Release pipeline runs all regression suites. Game layout lookup and full residential menu flow require in-game testing; no in-game success is claimed.

Test: use GO SEE for a plot without an estate/friend shortcut, initially within the same world. Verify the ward, residential shard, final walk, and right-click Stop. Then test subdivision and cross-world routes. Export immediately if a stage stops; housing route/step/interaction/missing-layout details are in travel history.
