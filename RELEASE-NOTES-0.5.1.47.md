Equinox Companion 0.5.1.47 — Garden update efficiency
For Journal V7.11.66; no new website deployment is required for this plugin fix.

The garden guide previously rebuilt source lists and scanned pending history on
unchanged frames. New observations also invalidated all cached batch projections.
This could add work at the same moment garden artwork changed.

- Reuse garden source lists and resolved observations until their inputs change.
- Group actions by estate address and physical patch; project only changed batches.
- Keep unaffected batches cached across tending, planting, harvesting, fertilizer,
  crop-status observations, mapping changes and incoming shared data.
- Cache pending-bed markers until observations or upload acknowledgements change.
- Preserve full-address/world/game-house matching, chronological replay, confirmed
  actions, stale-event guards, plan edits/reset, care timestamps and live timers.
- Existing artwork, shared texture handling and automatic upload behavior remain.

Validation: 802 automated checks passed; Release build has zero warnings/errors.
A synthetic 6,000-action history across 30 batches recalculates only one batch for
one additional tending event. 3,000 unchanged batch draws cause zero extra replays.
All 30 projected results match the previous full-history replay. Added checks cover
all listed action types, remote field changes, remapping, history pruning, world/
house isolation and reset. Existing garden/game-event/registration tests also pass.

No in-game FPS measurement was possible here. This removes confirmed redundant
work; live smoothness and possible remaining texture/config-save costs still need
Cláudio and Goddess's check. Update through the existing Dalamud repo.json feed.
No new pairing key, deleted history, or data reset is needed.
