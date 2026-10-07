# Companion 0.5.1.72 — Teleport completion and /et

- Complete an accepted native Teleport/estate request after an observed loading cycle ends in the expected World, territory, map and instance. This does not depend on landing within 15 yalms of the leader's sampled position. Rejected requests, cancelled casts without loading and wrong destinations cannot use this completion path. Doors and other interaction routes retain spatial validation.
- Clear previous stuck/pickup and movement-retry state after confirmed travel, including completed Lifestream World travel. Keep FollowThem armed and resume under the existing nearby/ready checks.
- Bound an unconfirmed post-loading arrival wait to 35 settled seconds, preserving existing expiry/recovery rules. Log native Teleport acceptance and detailed pending-arrival state every five seconds.
- Add /et as an alias for /equinox travel and /eqtravel, using the same full World names or unique prefixes (/et sir, /et raff). If another plugin owns /et, preserve it and retain the longer commands.

Evidence: .71 leader diagnostics show the World flag clearing at 03:32:50 and native Teleport capture at 03:33:48; follower dispatched at 03:34:00 but no arrival confirmation was retained. User reports teleport succeeded but WAITING persisted; Stop/Start followed by Gridania teleport resumed follow. These identify a completion/resumption failure, but the exact live blocker was not recorded. This release fixes restrictive completion/stale movement-state paths and adds evidence for any remaining failure.

Validation: clean Release build and automated regression gates. In-game acceptance remains pending.
Journal stays V7.11.81. No new Cloudflare deployment for .72. Duty crossings without detected loading remain unresolved as documented for .71.
