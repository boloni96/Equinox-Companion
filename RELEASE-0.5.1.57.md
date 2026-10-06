# Companion 0.5.1.57

Includes all six coffer styles from .56 and the FollowThem feedback fixes from .55.

New: Auto-open nearby treasure coffers, beside the appearance settings under
Settings > Features > Treasure Coffer markers. Disabled by default, saved only on
this PC. Disabling the parent Treasure Coffer feature stops automatic opening.
Minimap/main-map visibility switches do not change this explicitly enabled action.

Companion requests interaction with ordinary ObjectKind.Treasure objects within
2.5 yalms. It does not move the character, interact with portals/NPCs, accept
confirmation menus or roll loot. QuickLoot settings remain separate. FollowThem
never relays coffer interactions. Both clients can choose this option independently.

Auto-open pauses during combat, loading, occupied events, cutscenes, casting,
crafting, gathering, trading, incapacitation and open interaction/confirmation
menus. It uses the native line-of-sight check. Requests have a global two-second
cooldown, a three-second same-chest cooldown, and at most two attempts per chest
per visit. Failed attempts do not loop indefinitely or mark the chest opened.
Only existing game observations can turn the marker green. Exceptions pause
auto-open and show an error with an explicit Retry control.

No extra Journal data, uploads, remote commands or background observation loop.
It runs with the existing enabled-coffer observation tick. Journal stays V7.11.74.

Validation: 966 automated checks; Release build with zero warnings/errors.
Native interaction range, line-of-sight behavior, safe pauses and icon cleanup
still require in-game testing. No native/game acceptance PASS claimed.

Window controls: right-click FollowThem or QuickLoot DTR entries opens its matching Companion tab;
left-click retains its toggle action. Esc minimizes the focused Companion window
to its floating launcher (Companion, Gardening, Fashion, Welcome, New Character).
Popups get first use of Esc; explicit X/Close remains close. Native focus/keyboard
capture and restoration need in-game testing.

Gardening account/group headers now aggregate actual garden attention instead of
alternating decorative account colours. Ready/cared-for plus unknown stays green;
actual tending due is blue; risk/check keeps orange, confirmed dead red. Unknown-only
groups remain neutral. Pending-sync indicators and actual garden records are unchanged.
