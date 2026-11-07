# Dresser confirmation handoff — 0.5.1.495

Report 231651 on 0.5.1.494 records four gear-route attempts stopping with Dresser closed at phase 5 on Yeti Fang Ring of Aiming, approximately half a second after starting the individual storage phase. None recorded a successful storage receipt. Augmented Cryptlurker's Chausses of Healing was separately skipped by the live-copy eligibility filter. The armoire-only routine was not running in this report.

Before an individual row is selected, wait for dresser readiness within the existing bounded deadline. After a row is selected, process its confirmation using the existing agent ownership, container/slot/item and prompt checks even if the main dresser's general readiness flag changes. Never resend the row selection. Receipt verification waits for loaded storage data and still requires both disappearance from inventory and destination presence; it no longer interprets a main-window readiness change as proof of failed storage.

Record a bounded state sample when readiness changes, including the main agent, data flags, item-list presence and ownership of the confirmation. The report cannot prove which readiness flag changed or that the callback successfully opened its confirmation; the new trace preserves that distinction. This change does not bypass eligibility, dyes, materia, damage, duplicates or gear-set protections.

Validation: plugin build and existing gate regressions. No live game UI is available here; the actual ring confirmation and receipt need an in-game test.
