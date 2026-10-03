# Flowerpot colour observations and preview

Reviewed 2026-10-03. Live indoor flowerpot identification/sync is not implemented. The current colour cycle is a display-only Oldrose example in the artwork test views. It never writes garden observations, changes the crop's colour, or declares readiness.

## Evidence

- [English game text, pinned revision](https://github.com/xivapi/ffxiv-datamining/blob/d9582a624eaec69d45649366dee801c2f3a6e9b3/csv/en/custom/003/HouFurPlantPot_00331.csv), rows 16–18. Blob c4853ace61e8a2754f6ac342b009a0e4ebe1aa4a.
- [FFXIV Gardening colour guide](https://www.ffxivgardening.com/flowerpot-colors).
- [First-hand player discussion](https://forum.square-enix.com/ffxiv/threads/488178-Potted-Plants?p=6321464&viewfull=1). Community observations, not an official specification.

The extracted prediction templates read:

> Perhaps it will yield red flowers...

> Perhaps it will yield red flowers, or perhaps flowers of a more unusual shade...

These differ from the suggested “appears to be growing” paraphrase. The CSV export alone does not establish how every colour is substituted in the live client or which chat channel receives the text. Capture the actual menu/chat and target before adding a parser.

The guide describes red/blue/mountain pomaces as colour inputs. For Oldrose, red+blue gives purple, blue+mountain gives green, and red+mountain gives orange. After all three types, rare outcomes become possible, while the ordinary colour remains possible. Repeated equal-length preview frames do not imply equal odds. Do not infer an exact percentage from this material.

Flower exceptions matter: default colours vary; cherry blossoms use pink where many flowers use rainbow. Match both the game item catalogue and available artwork rather than enabling every colour file for every species.

## UI implemented in V7.11.44 / 0.5.1.32

- Artwork preview → Possible flower colours (demo).
- Oldrose: white for one second, black for one second, rainbow for one second, repeat.
- A visible possibilities label and normal-colour caveat remain alongside the image.
- The preview can pause. Website reduced-motion preference starts it paused; timers stop while its panel or page is hidden.
- The existing outdoor beds keep their real crop and care state. No flower colour is fabricated on those beds.

## Required before connecting real flowerpots

1. Establish stable indoor residence/room and pot identity; never reuse outdoor bed 1–8 coordinates for a pot.
2. Capture a newly planted Oldrose, each observed pomace action, the prediction after each action, bloom, and the actual harvested item.
3. Record actual game text and named crop together with its target and timestamps. Screenshots plus diagnostic exports are useful; ordinary outdoor-garden history is not guaranteed to capture indoor colour messages.
4. Keep predicted normal colour, rare-colour possibility and confirmed harvested colour as separate fields. A prediction does not identify the future harvest.
5. Use only supported species/colour artwork. When the outcome is confirmed, stop cycling and show that outcome. Health, watering and maturity remain independent.
6. If a pot changes crop or is emptied, clear the old prediction. Unusual-shade text never creates a watering event, maturity event or probability percentage.
