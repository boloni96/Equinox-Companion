# Navigation 0.5.1.470

Standard-control walking no longer reduces forward speed to zero whenever facing
lags the checked route heading by 0.25 radians. It now projects the checked world
heading into the character's current control axes at constant requested speed,
while character facing still turns smoothly. This also prevents a lagging facing
from carrying the character off the checked line. No camera rotation is added.

Removed the second world-heading smoothing layer and its turn-speed modulation.
Pursuit already chooses a collision-checked chord; the old extra smoothing could
replace it with a different heading based on only a 0.6-yalm probe. The movement
hook now follows the selected chord. Entrance steps still request half speed.
Manual input, game movement gates, right-click cancellation, interaction arrival,
collision checks and the 180ms fail-closed input lease remain unchanged.

Evidence: export equinox-test-20261107-004144, plugin 0.5.1.469. Recorded walks have
zero expired input reads. Standard-mode ending axes included forward magnitudes
0.255 and 0.509, consistent with facing-dependent speed suppression. This report
has no per-frame facing trace, so it cannot establish the cause of every pause.
Limsa interaction completed at 10.33 yalms. Goblet later failed a ground check near
(117.15,-17.00,-23.95); Mist's plot route timed out after 90 seconds/522 expansions.
Those physical/search failures are not claimed fixed by this movement change.

Tests reconstruct world velocity from Standard-control axes through small turns,
right-angle turns, reversals and heading wrap, verifying constant magnitude and
alignment with the checked heading. They also preserve zero/half-speed commands,
existing physical-collision pursuit, cancellation and input lease tests.
Release build and independent/Gate tests pass. In-game smoothness needs retesting.
