# 0.5.1.457 — reject unavailable regional journeys before starting

GO SEE checks destination eligibility before any native estate, friend or Lifestream address path. The native world-travel entry point applies the same guard to commands, shopping and automatic travel before creating a trip or logging out.

Eligibility uses the character's home region, never the current visiting region: JP/NA/EU can visit their own region and Oceania; Oceania natives remain within Oceania. Returning from Materia to the home region remains allowed. Unknown/non-public destinations are refused. Rejections report the home/destination worlds and are included in travel diagnostics.

Source verified 2026-11-06: https://na.finalfantasyxiv.com/lodestone/playguide/contentsguide/datacentertravel/
This checks regional eligibility, not temporary congestion or game-side availability. Those still require the live destination UI.

Validation: all home/destination region combinations (including unknown values), release build and release regression suites.
