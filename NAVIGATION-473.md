# Walking continuity 0.5.1.473

Consume waypoints crossed by the previous/current player segment, even when neither
frame ends within the 0.25-yalm radius. Use full 3D distance and ignore discontinuities
over 2.5 yalms. Consuming a point never bypasses the next physical movement check.

Waypoint advancement uses physical traversability rather than the optional wall-
spacing preference. That preference still influences pursuit's preferred chord,
but cannot keep an already passed route point behind the character as a target.

When a calculated continuation starts exactly at the current section endpoint,
append it immediately, preserving the current waypoint. Pursuit can look ahead
across the section boundary rather than aiming for the old endpoint before switching.
Disconnected continuations retain the prior checked handoff; no unverified link is
created. Manual input, stop controls, collision checks and the input lease remain.

Independent tests cover a waypoint crossed during a slower frame, blocked onward
movement, different floors, position discontinuities and continuous/disconnected
section handoffs. Gate tests and Release build pass. The supplied .472 export shows
Goblet successes and cached reuse, but does not trace every heading/frame; these
changes address reproducible code paths rather than proving every reported turn or
pause had the same cause. Mist route timeouts and exact plot-gate targets remain
separate unresolved work. Live walking smoothness still requires confirmation.
