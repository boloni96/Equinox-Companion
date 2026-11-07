# Navigation 0.5.1.483

Clear nearby route bends can now be bypassed using a complete live collision-checked connector. The waypoint index advances with that decision, preventing a rounded corner from sending movement back toward the old grid point. Checks are bounded to three candidates, 2.2 yalms per connector, and 4.4 yalms along the existing route; distant loops and other floors are not shortcut blindly.

Partial remembered routes can be reused in either direction. Discontinuities over 2.9 yalms terminate reuse. The saved route network now accepts observed endpoints within the requested arrival range, with the same vertical tolerance as movement completion, instead of requiring a connector to the exact object centre. Live movement checks remain active on reused routes.

Regression checks cover open grid bends, blocked corners, rounded-turn progress, separate floors, reverse partial reuse, and interaction-range endpoints. Prior reports establish successful plot arrivals but also lengthy planning and an Empyreum support-ray failure. This change does not claim to fix that support-ray failure or all planning delays; in-game validation is still required.
