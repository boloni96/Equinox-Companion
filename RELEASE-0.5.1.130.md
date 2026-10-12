# Companion 0.5.1.130

Shopping-list world comparisons now display **DC / Region** directly. The table can scroll horizontally in smaller windows, keeping the world column fixed.

Compare **Lower total est.** with **Lowest unit first** when their stack choices differ. Both show the full quantity purchased, extra or missing quantity, average gil per purchased item, total gil, stack details and Travel. For example, needing 3 may mean choosing 3 at a higher unit price for less total gil, or buying 99 at a lower unit price. Neither option splits a market-board stack.

World ordering still uses the lower estimated total for sufficient stock. These plans compare cheapest-unit stacks with a single sufficient stack, not all possible combinations. Tax is excluded and results depend on the listings returned by Universalis. Live UI validation remains necessary.

**Reset list** cancels queued work, clears saved shopping items and resets the import text. The **X** beside the single-item search clears text, selection and results and stops refreshing that search.
