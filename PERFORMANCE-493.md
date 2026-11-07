# Save and Organizer allocation reduction — 0.5.1.493

Report 212811 recorded long session peaks in snapshot capture, market and maintenance work, plus repeated Organizer drawing hitches while its transfer routine was idle. Process-wide allocation and GC counters suggest pressure but do not attribute all allocations to Companion.

Configuration saves now coalesce changes for two seconds from the first dirty edit and defer snapshot creation while the single writer is busy. Dirty edits remain pending and the next snapshot captures the latest state. Unload still flushes the newest state. This avoids repeatedly allocating snapshots that only replace a pending write. Snapshot cloning uses UTF-8 bytes rather than an intermediate UTF-16 JSON string; mutable collections remain isolated as before. This does not eliminate the cost of serializing the complete persisted history, and increases the ordinary crash-loss window for recent edits from 250 ms to two seconds.

Organizer gear-set display refresh is limited to four times per second, with immediate invalidation on character/protection changes. Actual transfer checks retain their uncached refresh. Inventory display grouping/sorting is cached for 250 ms and invalidated by stock identity and filters. Selection quantities and actual transfer reads remain live. Add separate timings for gear-set display, inventory rows and stock refresh to distinguish those from overall window rendering.

Validation: build and gate suite, including busy/dirty/due save gating, cache reuse/invalidation, existing configuration snapshot isolation and writer coalescing. No in-game FPS improvement has been measured yet. Market and maintenance session maxima remain separate profiling targets.
