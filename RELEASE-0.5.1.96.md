# Companion 0.5.1.96 — explicit event trades and solo battle entry

Requires Journal V7.11.91 for the new action types. Both clients need .96. Includes all .95 meeting, quest-state and loading-session changes.

## Event exchange

Open the Ironworks hand's Item Exchange confirmation, then Helper Controls. The leader's `Follower will buy the same` button sends that selected exchange once to the chosen active Quest Helper follower. There is no passive purchase capture and vendor steps remain forbidden inside recorded conversations.

Initial supported exchange: the six FFXV orchestrion rolls shown by the user, each costing one Unidentified Magitek. Quantities are bounded at 99. Match item IDs, names, quantity and exact cost in the follower's own shop; never replay a leader row number. The follower needs the same nearby NPC, current map/world, enough tokens and empty inventory slots. A shop category that does not open directly needs manual selection. Other exchanges remain unsupported.

Exchange is submitted at most once per request using the live enabled button event. Completion requires both received-item and spent-token inventory changes. A timeout never repeats a purchase. Pause, Stop and session changes discard pending work.

## Solo quest battle

Capture the leader's actual Proceed button event on a quest battle prompt, with the accepted quest and current sequence. Commit the recording before the leader leaves the area. The follower must have the same accepted quest, sequence and exact prompt, and `Accept duty-ready prompts` enabled. Submit Proceed once and separately observe loading plus duty entry. No party disbanding, combat automation, difficulty changes or cutscene skipping.

## Verification

Regression checks cover trade scope/cost/quantity, inventory confirmation, forbidden embedded purchases, duty prompt matching and quest progress. Windows CI builds against official references. Native game interaction remains to be validated by normal use. The earlier skip/YES crash test must not be repeated.
