# Companion 0.5.1.140

The /bid table now follows the Paissa column layout: District, Ward, Plot, Size, Price, Entries, Lottery Phase, Allowed Tenants, Last Updated and First Seen, followed by paired bids and travel actions.

Funnel buttons beside District, Ward, Plot, Size, Lottery Phase and Allowed Tenants open checkbox menus. Multiple selections are ORed within a column and combined across columns. Active filters show the selected count; All and None controls are available. Long ward/plot menus scroll. The separate filter row has been removed. Click the sortable headings to toggle ascending/descending order.

Lottery Phase offers Accepting Entries, Results, Unavailable, FCFS and Missing/Outdated. Allowed Tenants offers Free Company and Individual. Unknown entry counts remain unknown and expired lottery observations do not appear as accepting entries.

Opening /bid defaults to the character's current world, including World Visit, and initializes the world-picker region accordingly. Manual world selection is retained while the window is open; reopening resets to the current world. Home world is only a fallback when current-world data is unavailable. Existing paired bid identity remains tied to the bid's recorded world.

No Journal deployment change is needed beyond V7.11.166 for shared bids. GO SEE, Copy and shopping destinations are unchanged.

Validation: local Release build (zero warnings/errors) and regression suite, including multiple selections, combined columns, tenant flags, empty selections and FCFS/outdated phase classification. In-game visual verification remains necessary.
