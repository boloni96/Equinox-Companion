Equinox Companion 0.5.1.46 — Journal V7.11.57

- Automatically registers a new logged-in character under a uniquely recognized game account's Person/account.
- Otherwise opens New Character Detected. Choose Person, then Account. Account selection submits immediately; new names use Enter. No Add/Link/Save registration step or website approval.
- Automatically supplies home world, data center and region. Supports existing empty accounts, new Persons and new accounts.
- Known characters are preserved; content IDs and name/home-world fallback prevent duplicate registration. Ambiguous fingerprints are never guessed.
- Registration is persisted locally for retry and scoped to the pairing key. Character switching clears unfinished choices; Later dismisses for that login and /equinox register reopens.
- Success chat appears when the shared roster contains the character. The server can project accepted registrations with the browser closed; full Journal storage catches up during normal website sync.

Deploy Journal V7.11.57 and save it once to share the updated account list, then update this single plugin through repo.json.
Validation: 728 plugin checks, clean Release build, registration Worker/policy tests and website layout/rendering checks. Live popup and cross-PC tests remain for Cláudio and Goddess.
