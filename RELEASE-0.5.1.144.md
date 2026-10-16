# Companion 0.5.1.144

/bid now observes character and current-world transitions from the framework update loop, including while the window is hidden during logout. Loading clears old results and cancels outstanding requests. A nonzero character/current-world pair must remain stable for one second before selecting its world and region; home world is no longer substituted while current-world data is loading.

An open window immediately searches the new world once ready, independently of Auto 60s. A closed window does not issue Paissa requests; reopening searches the current world. Same-world character switches and world visits also refresh. Stable frames preserve manual world selections. Favorites, filters and sorting are retained.

Validated with a Release build and regression tests for logout, late/zero world data, same-world character switches and world visits. Live login timing still needs an in-game check. No Journal update required.
