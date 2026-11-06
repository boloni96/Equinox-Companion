# 0.5.1.443 — select native DC and world destinations

The destination dialog opened, but neither DC nor world selection took effect. Manually selecting Materia then Sophia allowed Companion's existing destination verification and confirmation to continue.

Replace ordinary list dispatch with LobbyDKTWorldList's region-relative tree input. Resolve the exact destination from the menu data, require a visible enabled matching row, send the indirect region/item payload to the correct list, and read back the selected DC/world before confirmation. Retry no more than every two seconds. Ambiguous or unavailable destinations are not substituted.

Regression checks cover DC region encoding, separate world-list indexing, missing/duplicate destination rejection and native input layout. Full plugin build and release CI are required. Live retest: select a destination such as Sophia from Light and let Companion select Materia and Sophia itself.
