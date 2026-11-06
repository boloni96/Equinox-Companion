# 0.5.1.448 — forward-facing standard navigation

The 447 export showed 48 movement reads and 48 overrides, zero expirations, native gate blocks, manual conflicts or replans, and a successful arrival. It did not include the control mode, so it cannot prove which branch was active. The standard-mode adapter previously supplied lateral movement relative to the existing facing, which permits sideways travel.

For standard controls, smoothly turn only the local character toward the checked navigation heading and ramp forward movement as facing aligns. Do not supply lateral movement in that branch. Preserve legacy camera-relative controls, the user's movement-mode setting, camera handling, native movement gates, manual-input priority, cancellation and the expiring input lease. In-place alignment does not count as a walking stall.

Ended-navigation records now include control mode, final movement axes, heading and alignment state. No further aetheryte distance changes in this update.

Validation: full build, navigation regression suite with facing convergence, no forward movement while facing sideways/backward, and smooth forward ramp; release CI runs all five suites. In-game verification of facing remains required.
