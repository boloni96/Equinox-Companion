# Companion 0.5.1.286

Crystal Entrust/Withdraw uses dedicated character, retainer and FC chest storage.
Retainer withdrawal no longer requires an empty normal bag slot for crystals;
it uses the quantity-transfer path and confirms the actual inventory change.
Only remaining crystal capacity is requested. Empty dedicated slots retain their
element identity and cannot be treated as free slots for another item/element.

FC chest Entrust now accepts saved Entrust plans, including crystal selections,
keep quantities and protections. The existing FC chest **Crystals** tab filter
can limit the operation to crystal storage. Ordinary item tabs cannot receive
crystals. Shards, crystals and clusters remain NQ; HQ-only never substitutes them.

Test in-game: withdraw crystals with full normal bags; entrust crystals to an
empty retainer crystal slot; repeat both directions using the FC chest Crystals
filter and permitted chest access. Try a near-capacity destination and verify
only available capacity moves. No gil, gardening or recipe-expansion rule changes.
