# Companion 0.5.1.450

The supplied 0.5.1.447 trace shows item 51997 consumed (1 to 0) while the generic unlock API remained negative for over five minutes. Mount 52261 also consumed before its generic unlock state turned positive about 27 seconds later. No blocking prompt was recorded. The trace does not establish the specific collection flag during that wait.

Reward verification now checks the mapped collection even when generic status is negative or unsupported, preserving a positive result from either source. Exact catalogue mappings are cached per catalogue instance; live unlock results are not cached. Consumption and a positive unlock remain required.

Waits report delayed confirmation after 30 seconds and stop after 60 seconds without repeating use. Automatic event-account progression requires no pending use. Export snapshots include raw generic, collection and resolved unlock states plus inventory counts. Shopping UI explicitly labels pending purchases.

Release build and Keybound regression tests pass; CI runs all suites. In-game validation is required for collection flag availability. No unlock is inferred from item disappearance alone.
