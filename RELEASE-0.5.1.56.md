# Companion 0.5.1.56

Choose a coffer appearance under Settings > Features > Treasure Coffer markers
> Coffer appearance. All six styles include red/unopened and green/opened previews:
Game chest, Classic chest, Rounded chest, Royal jewel, Pixel chest, Minimal outline.
The chosen style applies to minimap and main map. It saves locally and never pairs
or uploads. Existing installations retain the Game chest until a different choice.

Custom minimap styles use the existing native marker position/visibility. The
underlying native symbol is made transparent while the custom symbol is drawn,
and its original alpha/colour is restored when switching style, disabling markers
or when the live node is recycled. Native pointers are not cached/dereferenced
across addon destruction. The original native style remains available.

Only drawing and local appearance settings change. No new observations, scanning,
Journal uploads or coffer interactions are added. FollowThem remains as in .55.
Journal remains V7.11.74; no new Cloudflare deployment needed.

Validation: Release build, zero warnings/errors; 941 existing checks pass.
The six custom designs, native alpha restoration, map clipping and appearance
at different game UI scales still require in-game testing. No live PASS claimed.
