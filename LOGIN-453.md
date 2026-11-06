# Companion 0.5.1.453

Separate character-list routing for World Visit and Data Center Travel. If the character's current and home worlds share a data center, select its home-world list. Only a verified different current data center selects the visiting-world list. Apply this rule both to initial saved-location hints and live lobby current-world corrections, preventing a current-world hint from overwriting the home-list destination during same-DC visits. Character identity and service-account checks remain required.

Regression cases cover Golem/Rafflesia and Raiden/Phoenix same-DC visits, Rafflesia/Sophia cross-DC travel, home-world login and unknown current-world data. Build and gate tests locally; full regression suites in release CI. Actual lobby login still requires game validation.
