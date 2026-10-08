# Companion 0.5.1.97

Fix the two-minute wait shown in the 19:05 diagnostics and 19:08 screenshots. A leaveDuty instruction received while the follower was completing a solo battle remained queued after the game had already returned the follower to the overworld. QueueDutyLeave could no longer execute it, so normal following stayed blocked until expiry.

Discard an obsolete duty-exit instruction both on receipt and when revisiting the queue after loading. A loaded player outside the source territory with no current duty establishes that exit is unnecessary. Loading, unavailable player state and a transient cleared duty id in the original territory do not establish exit. Disabled assistance or a different current World/duty discards the inapplicable instruction. Valid same-duty requests retain the existing loot checks.

Discarding on receipt also prevents a late, already-unnecessary exit instruction from clearing a new quest conversation. No website update is required beyond V7.11.91 for the existing .96 features.

The supplied exports stop before the later Kipih Jakkya interaction. Screenshots show that interaction eventually approached, opened and progressed through dialogue. This patch fixes the demonstrated travel blockage; it does not claim to explain every missed subquest.

Cutscene skipping remains disabled. No crash reproduction or native in-game test was performed. Regression tests cover completed exit, loading, missing state, same-duty loot eligibility, disabled assistance and unrelated travel.
