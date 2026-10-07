# Companion 0.5.1.70 — Correct FollowThem native input return type

.69 could insert /, toggle walking, change targets/focus targets, or trigger other controls around stopping and travel. Its hook used a default-marshalled bool for a native one-byte result. The old delegate can read unused upper return bits as true even when the native input is false.

- Use an explicit byte return for both input hooks, preserving the original byte for every unrelated input. Only the exact MOVE_BACK action on the current UI input can be overridden during a bounded stop pulse.
- Disable the hooks when each pulse ends or cancellation is observed; retain per-pulse read diagnostics even when the first attempt succeeds.
- Remove the window keyboard-message fallback from stopping. If bounded native cancellation fails, hold travel and request a manual movement tap rather than post another input.
- Pause ordinary follow requests while an accepted, recognized party teleport is completing (bounded wait), keeping shared destination de-duplication.
- Keep .69 instance selection, door approach, queue recovery and relay spacing corrections.

Validation: Release build clean, zero warnings/errors; 1,192 automated checks pass. A native-call regression probe reproduces the old false-positive from nonzero upper return bits and verifies that the production byte delegate preserves both false and true. This is not an in-game test; .70 still needs focused acceptance.

.69 evidence: automatic instance 3 selected at 01:18:44, house/workshop/outside sequence completed, and stuck stop with text input active acknowledged at 01:22:42. Those successes did not make .69 safe from unrelated input side effects. The retained follower log used six native stop pulses and no window-key fallback.

Update both clients to .70. Journal stays V7.11.80; no Cloudflare update.
Test first: top-bar Stop and 10-second stuck timeout, with chat open/closed. Expect actual stop without slash insertion, walk toggle, unrelated target/focus or camera changes. Then party teleport once, house/workshop/outside, and instance selection. Export diagnostics immediately if side effects recur.

Native interop references: https://learn.microsoft.com/en-us/dotnet/standard/native-interop/type-marshalling and https://devblogs.microsoft.com/oldnewthing/20150817-00/?p=91801 . Native input declarations: https://github.com/aers/FFXIVClientStructs .
