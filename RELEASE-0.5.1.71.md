# Companion 0.5.1.71 — Travel capture recovery and World shortcuts

- Clear the World-travel suppression flag on arrival independently of a previous position sample, on character change, on expiry, and after Lifestream becomes idle. This fixes a code path that could suppress normal Teleport/estate observations for 30 minutes after World travel.
- Add /eqtravel with full names or unique case-insensitive World prefixes (/eqtravel sir, /eqtravel raff). Exact matches win; ambiguous prefixes list choices. /equinox travel remains supported. Non-World /li destinations continue to rely on normal native travel observation.
- Correct the Telepo observation delegate to preserve its native one-byte return; record accepted/rejected native calls, capture status and World suppression state.
- Retain departure position/direction across confirmed loading transitions inside the same duty, including separated same-territory arrivals. Follower validates the duty identity and uses the bounded direct approach. Requires Journal V7.11.81 relay support. No continuous position upload or Journal writes. Crossings with no detectable loading transition remain unimplemented pending evidence.
- Preserve .70 input-hook byte return and bounded stop pulses, party-trip deduplication and queue recovery.

Validation: clean Release build; automated gates and relay/API tests. In-game acceptance is pending. User reported .70 Step 1 passed for one attempt; no blanket claim about visual/input glitches. Ordinary teleport after World travel failed in supplied reports; logs lacked capture instructions, and the stale flag is a concrete code defect, not proof of every reported failure's cause.

Update both clients. Deploy Journal V7.11.81 to the same Cloudflare project for duty-crossing relay support. The teleport cleanup and commands do not require the relay update.
