# Companion 0.5.1.66

Corrects FollowThem waiting indefinitely after a rejected follow command.

- Send an explicit /follow <t> and recognise both old and current invalid-target/unavailable responses.
- Mark follow requested before issuing the command, preserving synchronous rejection handling.
- Keep captured travel after a rejected movement command.
- Prevent Start from re-arming while stop confirmation remains unresolved; align button and top-bar guidance.
- Allow real movement input followed by stationary confirmation to recover a missing cancellation notice.
- Preserve the .65 current-window backward-key cancellation mechanism and horizontal stuck timer.
- Remove alternating per-second queue notices; retain the original travel expiry.
- Reconcile queued aethernet legs already completed manually only at their recorded destination in a different source area, including world and floor checks.
- Record expected and actual source coordinates on mismatch and suppress duplicate callback diagnostics to retain useful history.

Validation: release build, zero warnings/errors; 1,141 automated checks pass.
In-game validation pending: crystal to shard to crystal, rejected follow during zoning, Stop and 10-second stuck pickup regression.
Journal remains V7.11.79. No Cloudflare deployment.

Known limitation: the original capture behind the reported source mismatch was absent from the diagnostic ring. This release adds evidence and safe manual-arrival reconciliation; it does not claim every travel route is fixed.
