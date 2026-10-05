Equinox Companion 0.5.1.49 — Optional QuickLoot
Journal stays V7.11.66; no website deployment required.

QuickLoot is disabled by default for new and existing installations.
Settings > Tracking > Enable QuickLoot shows its main tab. Disabling stops
rolling, removes the DTR entry and hides the tab, while retaining its rules.
Re-enabling leaves automatic rolling off until explicitly enabled again.

One shared configuration applies to all characters on this Companion install.
Unlocks, inventory and equipped gear are read from the current logged-in character.
No loot preferences or loot history are sent to Cloudflare.

QuickLoot tabs and nested tabs:
- Rolling: Current loot; Mode & timing.
- Filters: Collections; Equipment; Protection.
- Rules: Items; Duties. Search by name or ID; per-rule enable and action;
  bounded scrolling, clipboard export and validated import/replace.
- Feedback: Messages; Preview; History.
- About: thanks and source link for LazyLoot / PunishXIV.

Automatic rolling (FULF) and Top bar (DTR) controls sit at the top of QuickLoot.
The DTR entry shows Off, the current roll mode, or Paused for a LazyLoot conflict;
click it to toggle automatic rolling. It does not enable the master feature.

Need/Greed/Pass current loot buttons use a fixed snapshot of pending entries.
Automatic mode handles new entries while enabled. Separate configurable delays
apply before the first and subsequent rolls. Need falls back to Greed/Pass
according to the game's permissions. Do nothing leaves the entry untouched.

Filters: unlocked collectibles (all or individual categories, optionally only
untradeable), faded copies with all identified orchestrion results unlocked,
other-job gear, minimum item level, current-job average threshold, equipped-slot
item-level comparison, expert-delivery seal value, level-1 glamour exception.
Equipment comparison is item-level based, not secondary-stat optimization.
Weekly protection defaults on and takes precedence even over explicit overrides.
Otherwise item overrides beat duty overrides, which beat global filters.
Unknown item data is left for manual review. Unknown unlock data is not passed
as already unlocked. A held unique item follows game restrictions.

Queue guards: current character/territory/loot identity checked; cancellation
on character or area changes; no rolling while LazyLoot is loaded; unavailable
native interface stops rolling. Unacknowledged rolls stop after one attempt by
default. An explicit optional setting allows one failure Pass for the same
non-weekly entry. Acknowledgement is not a guarantee that loot was awarded.

Commands: /equinox loot opens the tab; append need, greed, pass, on, off, toggle,
or test <item ID>. Commands cannot bypass the disabled master switch.
ID previews assume normal permissions and no weekly restriction; current-loot
preview uses actual permissions. Neither preview submits a roll.

Validation: 804 automated checks passed; Release build zero warnings/errors.
The 728 existing garden/housing/sync checks remain passing. Gardening source
retains the 0.5.1.48 rollback behavior. Native FFXIV rolls, DTR clicks, game UI
layout and performance require live validation; not claimed tested here.
