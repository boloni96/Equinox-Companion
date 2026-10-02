# Equinox Journal V7.10.2 / Companion 0.5.0.2

Release prepared 2 October 2026. Recovered V7.10.1/0.5.0.1 source and continued work. Companion 0.5.0.2 is distributed through the existing GitHub installer feed. Journal V7.10.2 remains a manual Cloudflare package; publishing this plugin does not deploy the website.

## Changes in this checkpoint

- When automatic game application is enabled and the updated journal has saved once, linked house-entry times and authoritative estate names can propagate between paired plugins with the website closed.
- Private tenants do not reset owner timers. FC entries require membership. A verified FC change removes the old membership from the live view; absent FC information does not prove departure. Changed estate ownership removes the old owner timer until the full journal reconciles the house.
- This is a compact housing view. Full journal history, gardens, collections, new house discovery and unresolved links still apply when the website is open. No picture or full journal reads/writes are performed by the live roster route.
- Live observations are bounded to 5,000 cached identities; durable events stay queued if that cache is full. Website save races, old events, reused IDs and reviewed/dismissed events are guarded.
- Reconnect uploads use a fixed three-statement ingestion batch, including live projection updates, instead of one or more database calls per event. Conditional profile reads use the server's complete ETag.

## Validation

Passed: cloud storage/authentication regressions; companion event contracts and garden isolation; shared roster privacy and eligibility; new background two-client housing tests; a 50-event reconnect batch below the query limit; cache-cap and durable-event preservation tests; catalogue/event/Fashion/guestbook regressions. Companion compiles against the recovered Dalamud API 15 references with zero warnings and errors; synthetic gate tests pass.

Native game behavior and production Cloudflare CPU remain unverified. All 23 requested points are NOT yet complete. WORK-STATUS.md retains the remaining work.

## Installation

1. Export a journal JSON backup.
2. Upload the CONTENTS of EquinoxJournal-Cloudflare-V7.10.2.zip to the existing Cloudflare Pages project. Keep DB, PICTURES, the journal password, pairing key and stored data unchanged.
3. Open the updated website, sign in, enable automatic game application, and allow one cloud save to finish.
4. Update Companion from the existing main/repo.json GitHub feed when the feed shows 0.5.0.2. The same pairing key stays on both clients.
5. Test a private owner entry, a tenant entry, an FC-member entry, an estate name update, and second-client refresh with the website closed. Reopen the website to apply the detailed queued history.

## Remaining work

See the 23-point table in WORK-STATUS.md. Principal gaps: exact automatic Lodestone/portrait resolution; complete collection and event requirements/reward mappings; immediate harvest detection; full journal application while the browser is closed; guest photographs/automatic publication; and live FFXIV validation. Unknown character ownership still requires an explicit person/account link.

No pairing key should be regenerated and no queue cleared as a first troubleshooting step.
