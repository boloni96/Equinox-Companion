Equinox Journal V7.11.42 / Companion 0.5.1.30

Visitor garden setup
- The Garden Design sync icon appears at the upper right of /planting only for a uniquely identified paired character who is not linked to the current estate. It is hidden for owners/shared members/tenants and away from that estate's outdoor garden area.
- Click once to begin, then inspect Beds 1 through 8 in game in the displayed clockwise layout. No click on the sync button is needed between beds. Click it again to restart at Bed 1.
- The next bed number appears above the sync icon, on the center tile, and as a selected outline around the corresponding bed. Hover the button for instructions. Cancel discards unfinished setup.
- Fresh garden menus or recognized garden system messages record native target arguments and XYZ coordinates. A repeated target cannot advance to the next step. A numbered menu that disagrees with the requested bed/patch is rejected.
- Each inspected bed previews immediately using its observed empty/mature-crop data. All eight manual identities are saved together at completion. No crop name or care timestamp is invented from the selected plan or coordinates.
- A target farther than 8 game units from the first bed resets unfinished setup to Bed 1 and discards its temporary mappings. Return to the same patch to resume, or click the button to establish a new anchor. This is a conservative initial threshold requiring in-game testing. A nearby wrong bed may still require a manual restart.
- Changing character, zoning, changing the selected batch or leaving the matching estate cancels unfinished setup. Repeat setup if the garden is moved/replaced. Nothing is planted, removed, harvested or automatically clicked by setup.

Shared bed observations
- Numbered menus automatically teach bed identities for permitted characters; paired visitors can reuse those mappings or use the guided setup above.
- Recognize the English native system text 'There is nothing in this bed.' with fresh garden target evidence. Empty beds remain explicitly empty when shared back to Companion, including gardens without planting plans.
- Existing native mature-crop text supplies a crop name only when it matches a known game item. Numbered menus and confirmed actions retain existing ready/dead/tending behavior. If the game does not reveal a crop name, the crop stays unidentified. This release does not decode all growth stages from coordinates or chat.
- Shared mappings require the same estate, target argument and nearby recorded coordinates; stale, missing or moved mappings remain unresolved. Open the paired website during initial setup so it can create/save new physical patch links and reconcile observations.
- Observing/tending does not add the visitor as a house member, record a qualifying house visit, or subscribe unrelated characters to reminders. Original acting-character attribution is preserved.
- The supplied native-game exports replay successfully: eight empty beds and eight named mature crops. In both patches the eight targets share coordinates but have distinct target arguments. Coordinate distance guards the patch area; target arguments distinguish its beds.
- Garden artwork preview has moved from the planting guide to Settings > Diagnostics.
- New events require website protocol 10. Install this website update before expecting new observations to upload; the plugin holds them against older websites.

Existing behavior retained
- Each real character login can show one compact reminder again for that character's owned/linked houses. Unchanged reminders stay quiet during that login; changing areas does not repeat them.
- Confirmed harvest-ready chat remains independently switchable in Settings > Chat messages. All previous artwork, alignment, separate hover targets and shared garden style options remain included.
- The image inventory is unchanged in coverage; the new button reuses assets/icons/sync.png from Garden Design. Wired artwork still means an implemented rendering branch, not universal native-game verification.

Validation and installation
- Companion compiles for Dalamud API 15. Automated checks cover the eight-bed walk, repeat/wrong target rejection, restart, distance boundary, visitor-only eligibility, empty/crop/tending sync, absent planting plans, actor/house isolation, protocol validation, empty-bed reminders and stale events.
- Public seed and hosted/offline source consistency pass. The native game interaction and UI appearance still need an in-game test. The build completed with the existing unavailable NuGet vulnerability-feed warning.
- Upload Equinox-Journal-V7.11.42-Cloudflare.zip to the existing Cloudflare Pages project with its existing database/bindings. The website has not been deployed by the assistant.
- The Source ZIP contains source, tests and the optional offline page. Companion is published separately through the existing GitHub installer feed.
