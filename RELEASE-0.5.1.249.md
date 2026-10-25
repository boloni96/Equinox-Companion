# Companion 0.5.1.249 · Journal Market

Companion now uploads the observed selling lists, names, order, held gil and last
in-game visits of your retainers to the shared Journal. Enable Market tools and
keep “Sync retainers to Journal” on under Background checks & visits. Windows
and browsers can stay closed; the game must supply an observation before it can
be synced. Existing cached records retain their original observation times.

Deploy Journal and Background Worker 7.11.242 for the shared Market panel,
per-character Retainers section and six-day Retainer visits reminders. Older
servers safely hold these new records while existing gameplay sync continues.
Enable Retainer visits in the saved notification sets you want to use.

The upload outbox keeps one replaceable snapshot per character/retainer, with
receipts isolated by pairing key. Each retainer fits a separate bounded upload
envelope; gameplay records use available slots/bytes first. Unchanged siblings
are not resent. No game navigation or pricing action is started by this sync.

The Journal shows asking value, recorded gil and observed stock, not invented
revenue or profit. A Journal visit or market-price lookup cannot reset the last
in-game visit. No guaranteed selling-expiry time is claimed. Missing listings
alone are not sale evidence. Gardens and their detection/mapping rules are unchanged.

Validation: plugin build; transition/sync regressions including 19 retainer
checks; native text and seller tests. Journal source and both deployed scheduler
entry points tested with authenticated synthetic uploads and closed browsers.
Phone/desktop interaction and notification delivery use isolated test fixtures.
Real game-to-live-server delivery remains shared checklist N23/N24 after deployment.
