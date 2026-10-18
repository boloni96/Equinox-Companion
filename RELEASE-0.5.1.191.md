# Companion 0.5.1.191

Item references now use an ImGui popup above Companion. Removes the temporary overlay-hiding workaround. The popup has stable identity even if its originating list updates.

Actions: Where is it cheapest?, Do I already have it?, Copy item name, crafting recipe search, and (when Artisan is loaded) Open crafting lists plus explicit Craft one by recipe. Craft one uses Artisan IPC and rejects an already-busy Artisan. It only starts when selected.

Native game context-menu entries from arbitrary third-party plugins are not mirrored. Existing Equinox entries in actual game inventory menus remain unchanged. No Journal update required. Build checks do not replace in-game menu testing.
