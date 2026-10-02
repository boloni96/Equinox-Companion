# Character Progress — requested 2026-10-02

Status: requirements and source audit; not implemented or advertised as complete.

Expand the website's per-character Collection into Character Progress. Keep collections, progress and PvP together, with compact navigation and collapsible expansion groups. Preserve existing obtained/unlearned semantics, item icons, wishlist links and automatic sync. Never convert an unobserved feature into a false incomplete/zero value.

Requested from Altoholic screenshots:
- Collections: Adventure, Companions/Pets/Minions, Customization; orchestrions, tomes, Triple Triad and sightseeing vistas, plus all existing collection categories.
- Duties by expansion: dungeons, trials, raids, deep dungeons, guildhests, treasure hunts, ultimates, variant/criterion and chaotic content. Separate unlocked from completed.
- Events and rewards, grouped by year, including character/account comparisons with reward icons.
- Field Operations; Ishgardian Restoration; Island Sanctuary; Quests; Reputation; Custom Deliveries.
- PvP Profile: Grand Company PvP rank/experience; Crystalline Conflict season/current/highest standings and casual/ranked records; Frontline placements; Rival Wings records; current/previous Series level, experience and claimed rewards where observed.

Source audited: Sohtoren/Altoholic main (Plugin.cs, Models/PvPProfile.cs, Windows/ProgressWindow.cs, Windows/CollectionWindow.cs, Windows/PvPWindow.cs). No implementation copied into Equinox. Existing ALTOHOLIC-RESEARCH.md identifies this as the relevant modern repository.

Observed constraints:
- PvPProfile explicitly gates reads on UIState.PvPProfile.IsLoaded. Unloaded profile must retain last observation and request opening the game panel, not send zeroes.
- Island rank observed only on the player's editable island, not another player's island.
- Field-operation data is territory/state dependent (Eureka, Bozja and Occult Crescent); retain last known values outside those areas.
- Sightseeing uses adventure completion bits with a catalogue ID mapping; mapping needs verification against installed client version.
- Reputation uses tribe rank/current reputation reads; Custom Deliveries reads satisfaction ranks/allowances with quest eligibility.
- Duties use specific content unlock/complete mappings. Do not infer a boss kill merely from map discovery.
- Full expansion requires a bounded sync contract with per-section timestamps and protocol gating, catalogue IDs/icons, stale-observation guards, preserved historical event runs and unknown states.

Existing user priorities remain open: gardening house capacity -> planting batches -> beds, simpler automatically synced house gardening UI; remaining 23-point audit. This new feature does not replace those tasks.

2026-10-02 clarification: Altoholic is a reference for ideas and data-reading approaches, not a runtime dependency. Screenshots reaffirm expansion-grouped duties, PvP rewards, categorized collections including sightseeing, and character profiles. Version 0.5.1.12 adds direct profile fields; broader progress/PvP remains pending.
