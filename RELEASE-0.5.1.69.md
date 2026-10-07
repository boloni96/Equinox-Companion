# Companion 0.5.1.69 — FollowThem waiting and travel corrections

Repeated .68 testing exposed blocked queued instance selection, door range failures, duplicate party teleports and unreliable stopping while typing. Earlier isolated passes did not establish repeated reliability.

- Process an authenticated queued instance choice while its ready menu is visible over loading, before the normal loading gate. Reconcile completed door/boundary arrivals and discard unavailable dependent steps after a failed trip so a fresh independent request can proceed.
- Compute door/crystal approach distance in three dimensions while preserving the ground height. Require actual interaction range before dispatch and record range/identity details on rejection.
- Remember the uniquely resolved accepted party destination and observe its loading/arrival. Suppress the matching relay cast without relying on identical spawn coordinates; retain fallback when the party trip does not complete.
- Add bounded native MOVE_BACK input pulses scoped to the current game's UI input, character and pending stop. Retry after text input closes; retain acknowledgement and stationary checks. No target cycling or camera keys. The existing window-key fallback remains when native stopping cannot complete. This native behavior still needs in-game validation, especially while typing.
- Deduplicate overlapping boundary captures, space outgoing travel beyond the relay's two-second minimum, and retry only the specific short-spacing HTTP 429 response, with a bounded attempt count.

Validation: clean Release build, zero warnings/errors; 1,189 automated checks pass. These checks cover pure travel policy and geometry, not live game input/menu behavior. All .69 in-game acceptance cases remain untested.

Update both clients. Journal remains V7.11.80; no Cloudflare deployment needed. World shorthand remains deferred; use /equinox travel Siren for the controlled test.
