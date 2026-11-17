# Companion 0.5.1.580

- Open requested market History as soon as the current response and native History button are ready, without waiting for all listing packets or the 500 ms inventory polling cycle. Existing pricing verification remains unchanged. Failed button requests are bounded to 100 ms retries.
- Prefer one existing item home when entrusting, filling its partial stacks and empty slots before other assigned retainers. Separate NQ/HQ stacks remain separate.
- Consolidate shared-assignment duplicates into earlier existing holders when partial and empty-slot capacity can hold the source group. Keep unique items and capacity overflow in their current assigned homes.
- Regression coverage: split bag stacks, partial/empty home capacity, shared assignments, full homes, unique items and HQ separation.

Requires in-game validation of History latency and native transfers. Previous 0.5.1.579 workshop preparation, Info legend and filtered Clear all fixes are retained.
