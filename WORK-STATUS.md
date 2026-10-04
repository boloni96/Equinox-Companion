# Current release: Companion 0.5.1.44 / Journal V7.11.55

Person-level login garden reminders and character-page navigation; see RELEASE-NOTES-0.5.1.44.md. Previous audit follows.

# Current release: Companion 0.5.1.43 / Journal V7.11.54

Fashion artwork and website icon corrections; see RELEASE-NOTES-0.5.1.43.md. Previous audit follows.

# Current release: Companion 0.5.1.42 / Journal V7.11.53

See RELEASE-NOTES-0.5.1.42.md for the current update and verification. The earlier audit below is historical. Pending live checks: new ImGui artwork/layout, current-character house indicators, and old visitor beds without positional identity. No waiting garden observations were discarded.

# Latest release work — 2026-10-02

Companion 0.5.1.10 / Journal V7.11.18 implement house batches, inline planned planting, game guide via /planting, per-batch care warnings after 12 hours, crop-specific death estimates, and successful fertilizer observation. Read RELEASE-NOTES.md for setup and remaining native testing. Website package includes source and Cloudflare deployment archive; deploy manually. New-character account discovery remains awaiting user test.

# Current handoff

Companion 0.5.1.9 and Journal V7.11.17: automatic discovery through journal-scoped account identity, learned from a known character on each service account. Unknown/conflicting identities stay pending. Regulars use completed level-15 It's Probably Pirates; below that milestone, FC members are Floaters and confirmed nonmembers are Empty. Tests and build passed; game observation requires live verification. Website automatic application must be enabled and open to save new characters. Full release notes document setup and limits.

Gardening redesign remains pending: show all house-capacity batches and beds side by side, centre Start garden buttons, one inline planting planner under batches, automatic crossbreeding recipe plan separate from observed planting. Preserve all existing data.

Equinox Empire — 23-point status, 2 October 2026
Journal V7.11.11 / Companion 0.5.1.2

Implemented means source and local checks passed. It does not mean tested inside your running game. The whole 23-point list is NOT yet complete end to end.

1. Houses, paired owners and FC master: authoritative placard/ownership paths exist. NEW: dedicated Company Profile agent reader plus FC Members reader, stable observations, unsigned FC IDs, matched estate updates, no visitor membership. Needs live placard -> magnifying glass -> website test. Earlier checklist overstated dedicated Company Profile support; added in 0.5.1.2.
2. Gardening: simplified care summary, Tend & harvest, optional bed details and Plan or adjust. Game observations outrank manual imports. Still requires live tests of different batches, harvest/empty-bed refresh and members without harvest permission; crop-to-bed identification needs calibration.
3. Cloudflare/sync: small idempotent batches, held-record isolation and lightweight shared roster implemented. NEW FC events wait for website protocol 4, preserving normal older-version sync. Actual free-plan CPU and two-person production load not measured here. Full journal/garden/collection/FC profile application requires the signed-in website open.
4. Navigation: character/account navigation, browser history and selected pages implemented and locally tested.
5. Tasks/timers/Fashion: Current checklist merged into Tasks & Timers; expandable section, Add task, checkbox hover/hold explanations and manual fallback with newer conclusive game correction. Fashion records judged participation and score, without choosing 80/100 on the website. Live Fashion and timer checks remain.
6. Collections: unlock and equipment catalogues, hairstyles, Obtained with Not learned marker, personal storage locations and separate FC Chest availability implemented. Full equipment source classification remains missing. Unopened/unobserved containers are unknown; learned hairstyles do not prove race/sex usability.
7. Events: shared catalogue, Hide/Show, eligibility sync hints, yearly history mappings and canonical identities implemented. Complete quest/eligibility/reward coverage for EVERY event and rerun is not finished. Reused quest flags cannot prove a historical completion year.
8. Person naming: one Rename Person control in the sidebar, locally checked.
9. Plugin window: minimum size and Dalamud window controls implemented. Pin/opacity/blur behavior needs live interaction check.
10. Character and FC details: read-only full-width character facts, world/DC/region, inline collapsed jobs, Collection shortcut and clickable internal FC page. Lodestone identity matching, visible retry/error and sync-race fix implemented. NEW native FC profile details merge with Lodestone, preserve unknown/private data. Actual missing-character-detail case needs production retest; direct native CHARACTER Lodestone ID extraction remains unverified (name + home-world resolver used).
11. Plugin character website links: implemented; live signed-in navigation check remains.
12. Icons: blue Companion icon and blue Journal book, website favicon/footer, item/job/reward icons implemented. Installer question-mark outcome remains UNRESOLVED despite valid packaged 512px PNG/URLs. Do not label fixed without seeing the installer display it.
13. Guests: owner-selected characters/houses/photos, revocable links, protected galleries and moderated Guest Welcome Book implemented and authorization-tested. Guests see published selection snapshots.
14. Retention: acknowledged history older than 60 days pruned while current entity state and unsent actions survive. NEW FC profile records retain latest state per FC and reader. Long-running growth check remains.
15. Copy/buttons: updated to present behavior, including FC sync status and unmatched-profile review. Ongoing UI review, not a claim of complete proofreading in game.
16. Submarines: dedicated plugin tab and website voyage timers implemented. Actual workshop capture/return-time behavior needs in-game verification.
17. Automatic completion: quest/Fashion detection, Nocturne final quest and aliases, fresh-negative/manual correction and year-aware history implemented. User confirmed collections and historical entries working in some cases. All events/future mappings are not guaranteed; old reruns may lack occurrence evidence.
18. Rewards/wishlist: matched icons/item IDs, held/learned and recorded gear, grouped Lucian outfit and orchestrion rewards implemented and tested. User confirmed Regalia protection; all reward groups need coverage review. Unmapped/ambiguous rewards stay unconfirmed.
19. Pairing/reconnect: shared key, normalized startup, separate clients, retry/acknowledgment and incomplete-record isolation implemented. Two-client local tests pass; user/partner game restart and reconnect scenario remains to verify.
20. House chat/reminders: green owner/FC context and red 30-day reminders implemented. Live colors/names/eligibility check remains; recorded visits are not authoritative game demolition status.
21. Automatic linking: verified owned/paired-known estates create/link automatically; duplicate/unknown identity waits for review. NEW Company Profile reading does not join the visitor to that FC. Retest previously waiting paired FC houses after opening placard + profile.
22. Unknown estate/FC names: authoritative placard names and dedicated viewed Company Profile matching now implemented. Live scenario from supplied screenshot still needed.
23. Cloud picture entries: hashed-name explanation, preview and reference-safe cleanup implemented; shared/guest references and save races tested.

Next live check: deploy V7.11.11 first, open/sign into the journal and save once. Update Companion to 0.5.1.2 and refresh shared profiles. Open the FC estate placard, click the owner magnifying glass, leave Company Profile visible at least 5 seconds; then open the character's FC page on the website. Test an FC belonging to a paired character and a stranger: the visitor must never become a member and the visit timer must not advance from just reading.

Current checks: plugin build 0 warnings/errors; gate tests including unsigned FC IDs and mismatched estate rejection; server payload validation, stale observations, preserved voyages/storage, visitor isolation; browser FC game-field display and unmatched-profile inbox; hosted/offline parity. Live FFXIV and production Cloudflare not verified here.
