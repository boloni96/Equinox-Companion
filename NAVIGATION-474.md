# Mist street routing 0.5.1.474

Adds a narrowly scoped automatic street-corridor hint for the reported Mist
subdivision approach near plot 42. The reference street first descends below the
starting height, then turns through multiple stair sections to reach the upper
road. The generic parchment guide instead suggested a short diagonal approach.

The corridor is matched only in territory 339, within six yalms of both known
endpoints (including height), in either direction. Other districts/plot areas keep
the existing map/geometry search. Coordinates are factual routing landmarks from
https://github.com/NightmareXIV/Lifestream/blob/b36320170faafc8315f7585edb45cfbcd34e7046/Lifestream/HousingData.json
(territory 339, plot 42). No Lifestream code, runtime call or manual recording is
required. This is a specific street hint, not a claim of complete district coverage.

The native road-seed validator obtains actual floor heights and checks each step
against current game geometry. The bounded limit increases from 128 to 256 half-
yalm edges to fit the roughly 100-yalm detour; the per-frame budget is unchanged.
A blocked/unsupported corridor falls back to normal search, never unchecked movement.

Validation: tests replay the street topology with synthetic floor geometry, producing
203 validated samples and three expanded grid nodes. They check district, height
and endpoint isolation, every returned edge, and an impassable-corridor case. The
full independent suite and Release build pass. Actual geometry can differ and live
Mist completion is not yet confirmed. Plot GO SEE retains its existing approach
target; this change does not implement generic street-side entrance detection.
Includes the 0.5.1.473 crossed-waypoint and continuous-section-handoff fixes.
