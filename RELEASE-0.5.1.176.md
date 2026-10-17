# Companion 0.5.1.176

Adventure task hovers now format embedded sync timestamps as readable local dates and 24-hour times, without fractional seconds or raw timezone offsets. Long task details wrap within the tooltip.

Housing visits and estimates, garden care and harvest estimates, Fashion Report dates, inventory observations and submarine observations use the same explicit local-time format. Scores, task confirmations and stored sync timestamps remain unchanged.

Includes the three housing watch scopes from 175. Regression checks cover the reported Fashion Report timestamp, offset conversion, UTC timestamps, Portuguese locale, invalid dates and unchanged non-time details. In-game visual validation remains necessary.
