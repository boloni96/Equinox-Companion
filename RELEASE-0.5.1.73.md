# Companion 0.5.1.73 — Clear duplicate completed World trips

Confirmed .72 diagnostics show an announced World trip followed by a second arrival-fallback instruction. After Lifestream reaches Siren (04:09:13) / Adamantoise (04:12:20), the follower cannot replay the old source World, so its queue blocks normal following until expiry. Screenshot confirms automatic following resumes after that expiry.

- Retain an active-session World-announcement receipt independently of the ordinary-teleport suppression flag. Clearing that flag on arrival must not re-announce the same World journey.
- Suppress intermediate World-arrival fallback during an explicitly announced DC journey. Preserve fallback for unannounced menu travel. Receipt is scoped to pairing key, leader identity, destination and bounded lifetime; consumed on arrival and never stored in Journal data.
- Reconcile a queued World instruction when already at its destination, after loading and Companion-owned Lifestream travel finish. Check selected leader, active session and original freshness/expiry, then remove it and resume following. Later different destinations remain queued.
- Keep /et, /eqtravel and /equinox travel, .72 native teleport completion and .70 bounded input fixes.

Validation: Release build and targeted regression gates. In-game acceptance is pending. Journal remains V7.11.81; no Cloudflare deployment for .73.

Test: /et sir -> arrive -> leader moves; /et adam -> arrive -> leader moves; then /tp cloud -> /tp gri without Stop/Start. No duplicate World instruction should block following for two minutes. Export follower diagnostics immediately on failure.
