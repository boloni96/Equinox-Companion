# Companion 0.5.1.460 — repeated Goblet corner

The 0.5.1.459 report confirmed the game map loaded (map 83, road coverage 3.63%). It still accepted the westward segment at approximately (157.15, -26, -20.17), where game movement stalled. The route took 40.85 seconds to find; recovery then searched from the wedged position and exhausted its 15-second limit. The report did not expose enough collision samples to identify the exact collider/material.

Changes:
- Check movement/body segments in both directions to catch reverse-facing collision surfaces.
- On an observed stall, attempt a bounded backtrack along the actual travelled trail, checking each connector against current geometry. No random sidestep or collision bypass. Replan after reaching the retreat endpoint; stop if the retreat becomes blocked or exceeds six seconds.
- Keep observed blocked spots for five minutes within the same world/territory so another GO SEE request does not forget them. Saved routes/opening shortcuts cannot bypass these exclusions.
- Long recovery searches retain the normal one-yalm grid; half-yalm search is reserved for short approaches.
- Wait up to ten seconds for residential shard choices to populate. In the report, the first TelepotTown check occurred 22ms after setup while the list was still empty; it populated immediately afterward.
- Export path/trail/blocked positions plus bounded collision samples on a stall. Broader material probes are diagnostic only. Add a separate navigation/recovery performance row.

Validation: plugin build and independent regression suite pass, including observed-trail retreat, refusal of blocked/missing trail segments, scoped/expiring obstacle memory and reverse-facing wall checks. Release CI runs the remaining suites. The specific live Goblet collider and successful corner recovery remain unverified until the next in-game test.
