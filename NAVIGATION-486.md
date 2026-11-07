# Navigation 0.5.1.486

The 0.5.1.485 report and screenshots showed that the outdoor estate identity can be available before the player has finished crossing the entrance. Shirogane 15 subsequently reached and tended its garden, but Shirogane 10 stopped its entry early and then timed out planning a bed approach with no movement.

Outdoor entry now latches the current short segment when the requested estate first becomes identifiable. It finishes that segment before confirming arrival or handing off to garden movement, and does not keep extending segments after identification. GO SEE's teleport/friend and ward-entry paths use the same completion check. Ward travel cannot declare entrance arrival from identity alone without an entry segment.

Completion requires the matching estate, the correct height, and progress to the segment endpoint within the narrow entry corridor. A small forward overshoot is accepted so a slow frame does not cause a turn back or an extra entry step. Existing native collision/normal-input fallback and stall bounds remain active. This is an entry-segment completion rule, not a claim that the game exposes an exact gate boundary; live gate alignment still needs verification.

Regression checks cover early estate identification, missing identity, wrong floor, sideways drift, normal forward overshoot, and segment completion. Independent navigation tests, transition tests, and plugin build pass. Retest Shirogane 10 from the estate landing: finish walking through the entrance before turning toward the bed. No new per-plot coordinates were added.

Residential travel also compares direct walking distance with both shard walking legs plus a 30-yalm allowance for menus/loading. It no longer assumes that crossing the former 25-yalm cutoff justifies a shard detour. This is a distance estimate, not a guarantee of the shortest terrain route; collision checks still govern walking. Tests cover a nearby plot behind a shard detour and a long trip where the shard clearly saves distance.
