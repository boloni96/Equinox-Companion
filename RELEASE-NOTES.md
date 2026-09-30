# 0.3.0.0

Optional paired sync to Equinox Journal V7.9.14. Uploads confirmed house entries and tending in bounded batches, retries failed connections and deduplicates successful receipts. No requests when no events are queued.

Includes home/current server names and housing district names from game data for matching existing Journal records automatically. Name plus home server identifies a character; full address identifies a house. Physical garden patches are linked once in the website.

Existing local tracking remains available without sync. Pairing is opt-in and keys never appear in diagnostic exports. Planner, planting and harvest detection remain future work. See website setup guide before enabling sync.
