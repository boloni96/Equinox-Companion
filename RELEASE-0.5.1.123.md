# Companion 0.5.1.123

EQUINOX JOURNAL V7.11.130 / COMPANION 0.5.1.123

Faster, consistent game observation delivery
- New house entries and changed character, collection/event, Fashion Report, storage and workshop observations wake the plugin upload queue in about half a second when idle.
- Bursts retain at least 2 seconds between upload starts. Failed uploads retain backoff and local records.
- A visible, connected Journal checks new events every 5 seconds instead of 15. The active house garden stays at 2 seconds.
- Empty checks do not save or redraw. Unsaved edits, shared-save activity and conflicts still defer application safely. Hidden tabs resume checking when visible.
- Website request failures back off instead of repeatedly checking at the faster interval.
- Original observation timestamps and latest-data rules are unchanged.

Deployment: deploy this Cloudflare ZIP and update Companion to 0.5.1.123.
Checks: enter a known paired private/FC house with the Journal visible and no active edit; confirm the entry age and applicable roster checkmark update automatically. Allow network and page-save time beyond the polling interval.
Game discovery timing varies by observation: some collection categories rotate through scans, some information requires opening its game panel, and changed records cannot upload before detection.
If a delay remains, compare Companion Queue/Last receipt with Journal Game connection Position/server and waiting events.
No live paired account or native game was accessed during validation.
