# Preserve drafts during simultaneous testing

The shared testing checklist retains the result revision that was visible when notes were edited. If the same tester edits that test on another device, submitting the older draft requires review rather than silently replacing the newer notes. Retrying after review explicitly uses the latest result.

Includes the /eqtests checklist, per-tester Pass/Fail/Later/Not tested, shared history, offline retry and export introduced in .237. Requires Journal V7.11.220; no background Worker update. Gameplay and garden observations are unchanged.
