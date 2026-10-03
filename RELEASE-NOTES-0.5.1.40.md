Equinox Companion 0.5.1.40

- Gardening replaces the Planting main-tab label. Person/account/Regulars/Floaters groups start collapsed, with concurrent blue tending and green harvest indicators and orange risk warnings. Batch tabs show the same indicators.
- /planting gains a collapsed Saved plans section with both people's synced favourite crops and recipes. Selecting one applies its plan immediately. Swap crops, Reset plan and Undo stay at the bottom; there is no Save step. Requires Journal V7.11.51, refreshed and saved after upload. Plan edits preserve actual crops, planting and care records; stale edits cannot replace a newer plan.
- Submarines now has Persons subtabs with collapsed account, group and character sections.
- Private-house and FC-house shortcuts appear for the logged-in character only when the corresponding house is known. The FC icon has an FC badge. Status borders use the existing housing rules, with qualifying-entry and demolition estimates in the hover. Each checkmark lasts exactly seven days after its qualifying interior visit, independently of the other house, then disappears.
- Fashion Report hovers include this character's current-week Fashion Points out of 100 and recorded completion information.
- Royal Kukuru / Royal Kukuru Bean(s) and other explicit crop aliases retain their existing planting and tending clocks. Unknown or contradicted growth timing uses neutral seed artwork instead of a large growing crop.

Validation: 667 automated plugin checks; release build with zero warnings/errors. Matching Journal tests cover immediate plan save/swap/reset/undo and stale-edit rejection, authenticated event validation, unchanged real garden records, historical alias recovery, compact mixed-status batch summaries, full grouped job names, collapsed collections, Fashion Points hovers, mobile layouts and clean hosted/offline parity.

Includes the previous obtain-chat harvest detection, exact-bed tending fixes and FC-owner Floater exception. Native in-game UI and callback behavior still requires testing in FFXIV. Reopen an affected numbered bed to refresh health; previously missed tending times require a new confirmed tend. The update recovers erased alias clocks only where recorded history proves them.
