# Companion 0.5.1.93

Changing a pairing key caused acknowledged local history to be replayed oldest first. New actions could wait behind thousands of duplicates, while the UI called all acknowledgements "Sent".

- Upload batches now balance recent observations and older recovery records, with alternating first priority so large records in either group can progress. Selected records remain chronologically ordered within each batch; registrations are applied first.
- Successful batches normally retry after five seconds; garden activity uses two seconds. HTTP failures retain the existing backoff. Records are acknowledged only from validated server receipts.
- Journal V7.11.90 receipts distinguish new stored records from duplicates. Older Journal receipts remain supported and are explicitly labelled as potentially including duplicates.
- Pairing and Diagnostics show pending, eligible and held counts and the last receipt time. Diagnostic exports include the last batch's timestamp range, endpoint and sync status, never the pairing key.
- Automatic cutscene skipping is disabled: no opening or confirming native skip dialogs, no leader skip relay, and no skip permission when starting a session. Skip manually on each client. The callback path implicated in the reported crash has been removed; native in-game stability is not claimed from automated tests.

Keep the existing pairing key and saved records. Update both clients. Deploy Journal V7.11.90 for the new receipt counts and server-position information. Do not repeat the crash test.

Validation: queue/receipt regression tests and the existing source-build gate run before package/feed publication. Journal tests separately cover replay receipts, fresh-event delivery, authentication, cursor diagnostics and stale-data protection.
