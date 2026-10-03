Equinox Journal V7.11.39 / Companion 0.5.1.25

Corrected garden artwork layout
- Restore the original framed top-right overlay, at native scale and position, as the sole hover/focus/tap information target.
- Use the standalone icons family for actual bed care/uncertainty, lower-right ABOVE the full-width caption. A saved plan no longer duplicates the plan symbol in both corners.
- Preserve source artwork pixels. Icons and text do not cover the bed border.
- Use supplied assets/borders/different.png for the wrongly opened bed; no extra red outline. A wrong-bed or plan mismatch uses warning.png in the lower-right status position above the caption.
- Use supplied assets/borders/next.png for the correct next bed; remove the added NEXT word from the tile. The main instruction above the garden still explains which seed/soil/step to follow.
- Starter/replant badges stay independent. Secondary match/fertilizer overlays are not stacked into the crowded right-hand corner; details remain available on hover.
- Confirmed crop removal and other logic from 0.5.1.24 remain included.

Rechecked docs/01-LAYERS-AND-LAYOUT.md, docs/04-PLANS-AND-NINE-STEPS.md, state-picture-map.json and docs/07-ACCEPTANCE-CHECKLIST.md in the original Garden Design handoff. The different border identifies plan mismatch independently of care, while next is a guide instruction. Opening a different bed does not change its saved crop or care state.

Validation: plugin builds; website desktop/mobile custom-palette checks pass for image loading, top-right-only hover/focus/tap, centred rows and icon placement above captions. Catalogue/state tests and hosted/offline parity pass. Native FFXIV visual verification still required.

Upload the Cloudflare ZIP inside this package to the existing Pages project. Preserve bindings, database and pairing configuration. Website not deployed by the assistant. Source ZIP includes the optional standalone page. Companion is published separately through its existing GitHub installer feed.

Remaining artwork inventory distinguishes missing runtime mappings from alternative artwork and authoring references. Precise growth stages, confirmed death/wilt, storage pause, flowerpot colours and icon-only native item-selector highlighting remain incomplete; this visual correction does not claim them completed.
