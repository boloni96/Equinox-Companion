# Organizer 0.5.1.504

Game-inventory Shift/Alt picking now changes the temporary bag selection, using the available bag quantity and preserving manually edited quantities on repeated hover. It is active while Organizer is open, suspended during text entry and inventory operations, and never auto-saves picks into a preset. Plan and temporary counts are separately labeled; the temporary count opens My inventory. Previously listed bag items can be explicitly copied into the temporary selection.

Entrust errors survive nested action wrappers. Missing assignments are shown before starting and explain the choices; assignment mode never silently chooses an arbitrary retainer. Completed/blocked Organizer status remains visible, including selected items with no assigned destination. Gear-set checks, quality/trade filters and transfer checks remain in force.

Validation: new pure regression coverage exercises quantities, HQ/NQ copies, crystals, repeated hover, removal and assignment scope. Release workflow must pass all regression suites and plugin build before packaging. Live in-game interaction is still required to confirm the complete UI flow.

Outstanding earlier reports: garden starts rejected by local collision, excursions outside plots and intermittent frame hitches are not claimed fixed by this Organizer patch.
