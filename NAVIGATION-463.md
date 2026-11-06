# Companion 0.5.1.463 — capture unfinished route search

The 0.5.1.462 live report still timed out at Goblet East without moving toward plot 22. It expanded 534 nodes and yielded 1,866 times. Recent navigation timing fell from about 7.57ms to 2.42ms per update, but that did not establish a working route. No reward-use attempt was present in this report.

This diagnostic update preserves the unfinished search in exports, both while running and after stopping: sampled explored ground, recent rejected candidate positions, closest reached path, pending frontier count, ground/traversal rejection totals, native query count and ray-cache hits. Coordinates serialize explicitly. Samples are bounded to 512 explored points, 128 rejected points and 256 closest-path points.

There is no movement-policy or clearance change in this release and no claim that the Goblet route is fixed. A report exported after 15–20 seconds of planning is sufficient to inspect progress; the user can then cancel using the existing right-click stop.

Validation: plugin build and independent navigation regression tests, including bounded diagnostic samples and coordinate serialization. Includes the previous Framer's Kit corrections.
