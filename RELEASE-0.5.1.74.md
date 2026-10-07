# Companion 0.5.1.74 — Party teleport names and coloured category headers

- Resolve party Teleport destinations against the game's Aetheryte data with case-insensitive, collapsed-whitespace and optional leading "the" matching. Require a unique destination; do not guess ambiguous names. Applies across the destination list rather than a Gold Saucer exception.
- Keep an accepted party trip on hold for up to 45 seconds even when its destination name cannot be resolved. Observe its loading/arrival and use the actual World, territory, map, instance and bounded receipt timing to suppress a matching relay duplicate. Different destinations or later trips are not suppressed by that receipt.
- MAIN/ALT headers now show aggregated housing colours. Regulars, Floaters, Empty and Pending sync become coloured, collapsible headers using the same Private-left / FC-right housing urgency rules as characters. Save expansion choices; search temporarily opens matches. No new configuration schema is needed.

.73 user acceptance: World travel, ordinary teleports, shard/crystal, FC/house/workshop/outside, private chamber YES, Dohn Mheg crossing and automatic follow resume, combat wait/resume, Stop, stuck timeout/pickup and typing during timeout all passed in reported tests. Party Gridania, Ul'dah and Radz-at-Han skipped duplicates correctly. Gold Saucer doubled twice because "the Gold Saucer" failed the exact-name lookup and recorded destination zero. .74 addresses this confirmed failure.

Validation: clean Release build; regression gates cover name variants, ambiguous/unmapped names, bounded unknown-party hold and observed-arrival duplicate matching. This is not an in-game test of every map or language. Party confirmation recognition remains English.
Journal remains V7.11.81; no Cloudflare update. Update both clients; test party Gold Saucer once and Gridania once, then category colours/collapse persistence/search. Existing .73 fixes remain included.
