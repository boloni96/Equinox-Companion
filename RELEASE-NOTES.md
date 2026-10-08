# Companion 0.5.1.107

Includes 0.5.1.106's exact-match recovery for a follower who opens the recorded NPC dialogue as the recording arrives.

- Capture quest dialogue locally across a brief follower-status gap (up to 60 seconds since the last authorized status).
- Send only when the original recipient sessions are freshly authorized. Pause, stop and revoked quest permission remain blocking.
- Keep queued quest recordings while waiting for readiness instead of dequeuing and silently dropping them. Expiry is bounded to 60 seconds and logged.
- Log per-follower readiness details when a click cannot be shared, without logging the pairing key.
- Regression coverage for stale/fresh status, session replacement, pause/stop and permission boundaries.

Questionable's In the Dark of Night quest path and interaction handling were reviewed. Per-sub-objective completion flags remain a future integration; no external quest database was imported.

Build/test success does not establish in-game success. Cutscene skipping remains unresolved; keep it disabled. No Journal deployment required.
