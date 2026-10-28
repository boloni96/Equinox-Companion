# Retainer Retriever 0.5.1.264 validation

Release build with .NET 10 and official Dalamud API 15 references: zero warnings,
zero errors. GateTests: 1,798 PASS lines, including 17 new retrieval checks.
SellerTests: 324 PASS lines. Native suites: 56 Gil Trader, 6 lottery text,
28 item shortcut, 93 all-inventory/stack, 28 trade timing, 47 garden label,
11 seller, and 24 Gil Trader UI checks reported passing.

New checks cover exact finished-item quantities, explicit ingredient expansion,
recipe yields/cycles/ambiguity, multipliers, presets, whole-list imports,
invalid item rejection, quantity limits, multi-retainer partial withdrawals,
subtract-bags behaviour, missing acknowledgements, context changes and cancel.

Native menu callbacks, layout, in-game stock arrival and AutoRetainer handoff
still require live acceptance. No live retrieval was performed during development.
Use the short test sequence in RELEASE-v0.5.1.264.txt before a large workshop run.
