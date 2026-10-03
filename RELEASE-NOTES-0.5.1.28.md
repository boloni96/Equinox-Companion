Equinox Journal V7.11.41 / Companion 0.5.1.28

Garden layout and artwork
- Lower-right actual-care symbols and their hover targets sit two pixels above the caption background, including when resized. Top-right retains plan/planting information. No whole-bed hover.
- Solid unknown/planned/dead shade layers now cover the centered soil footprint. All 1,565 installed runtime PNGs passed dimension/readability checks; source pixels are unchanged.
- Use the supplied selected border for a selected bed; confirmed death uses the dead border, dead crop and dead badge. Different/unknown badges also supply lower-right status information.
- Top-right artwork follows planting progress: plan while pending, matched/different after observation, actual care after completion. A timer estimate remains distinctly labelled and uses estimated art.
- Choose Wooden or Simple garden frames and optional Corner trim on the website. These journal-wide appearance settings sync to Companion.

Confirmed death
- New protocol-9 garden.dead observation from the complete numbered English removal-only menu (Remove Crop, Quit). Remove Crop within a healthy multi-option menu does not qualify.
- Manual website correction can record a crop confirmed dead in game. Confirmed-dead crops stop tending/harvest estimates and manual tending actions. Crop identity and observed actor are preserved.
- Subsequent confirmed clearing makes the bed empty; a later planted/tended observation replaces old death evidence. Stale events do not overwrite newer confirmation.
- Deploy this website version before expecting the new event to sync; Companion holds it while the connected website reports an older protocol.

Quiet colored garden chat
- Green: confirmed ready to harvest. Blue: tending/check-care. Orange: estimated danger or a maturity check. Red: confirmed dead.
- Only the current character's owned or explicitly shared/member/tenant houses contribute. Sharing a pairing key or tending a bed does not subscribe a visitor to alerts.
- One compact summary per notification pass. Multiple houses use counts rather than one message per house or bed; a single house keeps its name and batch numbers.
- Unchanged warnings stay suppressed across character switches for this plugin session. New crop/care states and urgency thresholds can produce a new summary. Restarting the plugin resets that session memory.
- These are local chat notifications, not outgoing messages to other players. Existing message switches remain available, including a new confirmed-dead switch.

Research and remaining images
- Reviewed the supplied Lotlab Gardening Tracker source; details are in GARDENING-TRACKER-RESEARCH.md. Seed arrays suggest future unknown-crop identification, but its logged state bytes do not provide verified growth-stage mappings. No old packet offsets or GPL code were imported.
- The remaining-artwork inventory removes files wired in both apps and keeps unused, partial and reference entries. Precise seedling stages, flowerpot colours/pomace and some alternative artwork still have no automatic mapping; in-app preview lets you inspect all installed images.

Validation
- Companion builds for Dalamud API 15. Garden state, event ingestion, stale-event protection, actor/house boundaries, reminder eligibility, color selection and summary compaction checks pass.
- Browser checks pass at desktop and phone sizes for loaded artwork, centered garden rows, separate hover/focus/tap targets, lower icon placement, selected borders and shared style controls.
- Public website seed is empty; hosted and optional offline source code match. Native game appearance/menu behavior still requires in-game verification. Build's NuGet vulnerability feed was unavailable; compilation and tests completed.

Installation
Upload Equinox-Journal-V7.11.41-Cloudflare.zip to the existing Cloudflare Pages project. Preserve bindings, database and pairing configuration. The assistant has not deployed the website.
The Source ZIP includes the optional standalone page and source/research/audit files. Companion is published separately through its existing GitHub installer feed.
