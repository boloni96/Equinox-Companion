# Shared navigation 0.5.1.488

The 065356 report showed Shirogane W15 P15 taking 45 seconds without moving, then a retry spending roughly 41 seconds planning before its caller stopped the newly moving character at 45 seconds. The final successful care run was /gardening, not an Adventure success.

Changes apply to shared ground navigation:
- Geometry searches can publish collision-verified closed-node ancestry as a walking prefix, not only map-guided searches. A section must advance toward the destination, span at least three yalms of checked route, and remain within a bounded twelve-yalm section. The search commits its endpoint and retains ancestry so final paths include the already issued section. Detours that do not yet make sufficient destination progress still require more planning.
- Exact ray caching evicts the oldest entry at capacity rather than clearing all 60,000+ cached rays between slices. Live steering still uses fresh collision decisions, and no physical clearance rules are relaxed.
- A verified walking prefix starts the walking phase deadline. Observed movement refreshes that deadline; pure planning remains bounded at 90 seconds. Existing short stall/recovery checks and overall travel limits remain.
- The common native garden caller now follows the shared phase deadline, capped at three minutes, instead of imposing 45 seconds including planning. Both Adventure and /gardening use this caller. Plot teleport entry remains bounded at 90 seconds; menu/action confirmation windows are unchanged.

Validation: plugin build, independent navigation regression suite including mirrored streaming detours and final-path ancestry, phase/progress deadline tests, and gate suite. No in-game improvement claimed yet. Retest Shirogane W15 P15 through /gardening and Adventure separately; inspect planning time, first movement, and completion.
