# 0.5.1.148

My Empire previously rebuilt character lists and scanned full discovery/visit history for every estate on each rendered frame, including collapsed rows.

- Cache the local character/estate display projection, invalidating on saved changes, record counts or current-character changes.
- Index discovery and visit histories by estate/character; compute eligible entries and other visits once per rebuild.
- Keep live age/status calculations, search, saved ordering, hidden characters and current-character priority.
- Add My Empire draw and history-index timings to performance diagnostics.
- Preserve existing private-owner/FC-member entry rules; no saved data format or Journal change.

Validation: build and regression suite, including indexed-versus-original eligibility, updated entries, other visits and 10,000 unchanged cache frames. Actual FPS requires in-game verification.
