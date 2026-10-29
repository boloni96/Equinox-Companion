# Companion 0.5.1.285

Crafting-log right-click menus now offer **Withdraw recipe ingredients…** and
**Withdraw raw materials…** when Equinox Organizer is enabled. The Companion
item menu offers the same actions for craftable items.

Recipe ingredients withdraws ready-made direct materials for one result; raw
materials expands the subcraft tree. Both subtract matching bag contents and
open a nearby, targetable summoning bell on the character's home world before
using the existing retainer withdrawal workflow. No crafting is started.
For larger quantities or quality filters, use Organizer's existing controls and
Withdraw. That button can now open the nearby bell too.

The previous draft can be restored; saved presets are not modified by the quick
action. Ambiguous recipes require a choice before any withdrawal. No nearby bell,
world/character changes, combat, a competing task, cancellation or a 30-second
opening timeout stop the request. AutoRetainer is paused before bell interaction
and restored using the existing owned-pause logic.

In-game checks: choose direct ingredients for gear with a cloth/ingot subcraft,
then raw materials; verify different items and correct bag subtraction. Test
Cancel, no nearby bell, both appearance styles, and AutoRetainer restoration.
Native bell interaction still needs live game verification.
