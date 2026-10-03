Equinox Companion 0.5.1.33 / Journal V7.11.45

Gardening supplies
- Required seed and soil are outlined in green in the native Gardening window and its item picker; identified alternatives are red.
- Read exact item/slot identity from native tooltips first. Icon artwork is a fallback only when all offered items sharing it have the same required/not-required result. Shared seed icons never imply an exact identity.
- Existing house, batch, numbered-bed, next-step and empty-bed checks remain required. Markers are informational and do not select items or perform game actions.
- Step 1 of a 1+9 plan leads with Potting Soil. Final Grade 3 Thanalan Topsoil is labelled as the later step 9 requirement.

Starter removal
- After the other seven beds are confirmed, Bed 1 shows the existing replant badge at the bottom right and its replant border. Its hover explains Remove starter → replant for step 9.
- That pending starter does not request tending or emit care reminders. Other beds retain their own care state and reminders.
- Actual crop records stay intact until the game confirms removal. Observed empty changes the instruction to planting; final planting clears the action badge and resumes normal care.

Tending appearance
- Droplets mean tending is needed, including recoverable wilt. They pulse one second on/off when animation is enabled.
- After recorded tending, droplets disappear. Wet soil remains for the 12-hour care window and the crop keeps its growth-stage sprite; the lower icon returns to growing.
- Live art remains for unplanted plans. Seedling/growing/mature/wilt/dead state priorities remain intact. Watering does not reset growth.
- The bundled -wet crop sprites remain available in the artwork inventory/preview but are no longer used on live beds, because they contain droplets. The separate wet-droplets effect is used for tending requests.
- Website matching changes are in the direct-upload V7.11.45 Cloudflare ZIP. This release does not deploy the website.

Validation
- Release build against Dalamud API 15: no warnings or errors.
- 470 C# checks, including 1→8→9 progression, confirmed removal, soil selection, shared-icon ambiguity and droplet suppression.
- Website checks cover actual step transitions, independent plan/care targets, all artwork references, wet soil versus due droplets, one-second animation, reduced motion, shared settings and existing links.
- Native game rendering cannot run in this environment. Check the Gardening slots and seed/soil picker in game, including wrong items, step 1 Potting Soil and step 9 Thanalan soil. Also check that closing the menu removes the outlines and the wrong bed is never given supply markers.
