# Shared testing checklist

Open `/eqtests`, `/equinox tests`, the window launcher or Diagnostics → Testing checklist. Pick a tester, read the steps, and record Pass, Fail, Later or Not tested with notes. Results sync through the paired Journal; the browser need not be open. Different testers keep separate reports. Pending reports persist locally and retry; stale edits from the same tester require review. Changed test revisions keep earlier history and request a retest.

The compact window keeps its controls above scrolling content and supports the existing minimize launcher. Export results includes definitions, downloaded report history, tested versions, notes and local pending reports. Open exports folder locates the file directly. No pairing key, private character roster or inventory records are exported.

Requires Journal V7.11.220. Scheduled Background Worker V7.11.217 does not need redeploying. Without the Journal update, the panel explains the requirement and the existing gameplay sync is unaffected.

Validation: pure policy tests cover per-tester summaries, immutable snapshots, idempotent receipt merging, changed test revisions, pending-report retention and avoiding config saves on unchanged polls. Full build/regression results are recorded in GitHub Actions. Live in-game rendering remains to be tested.
