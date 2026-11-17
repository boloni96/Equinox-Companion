# Organizer and workshop 0.5.1.579

The Organizer footer now has one compact Info hover containing the shared material-color legend and NQ/HQ-set explanation. Crafting no longer prints the legend inline. FC workshop uses the same color function and preference and shows Bags and Retainers / FC allocations alongside Need, Craft and Missing. Source details remain on item hover. Material colors can be changed from global Organizer Options.

Withdraw + turn in now reviews remaining queue requirements before the first material delivery (after reading an existing project's actual phase). It subtracts bag allocations and excludes delivered sets, selects complete NQ or HQ sets under the chosen filter, and groups withdrawals by recorded storage source and quality. Multiple materials are retrieved during each storage pass. It returns to the station only after allocation receipts. Missing turn-ins stop for preparation instead of silently starting a partly supplied queue. Capacity checks account for existing stack room and reserve one finished-part slot; smaller project batches, then the current phase, are used only when the whole queue does not fit. Later shortages trigger another queue preparation review instead of a single-row trip. Stock changes or failed transfers stop with the queue preserved.

Workshop navigation reuses routes only after observed arrival, matching territory, destination, endpoint range and a traversable entry connector. Live next-segment collision checks remain enabled; blocked connections replan.

Tests cover multi-item batches, split FC/retainer sources, quality isolation, complete delivery sets, delivered counters, changed stock, stack capacity, and lower-floor route reuse. In-game full-queue withdrawal and layout verification are still required.
