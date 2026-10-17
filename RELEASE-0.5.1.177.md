# Companion 0.5.1.177

Manual Check watches now retries unavailable worlds after 30 seconds, then 60 seconds, 2 minutes and at most once every 5 minutes until successful. Network failures, timeouts, rate limiting and server failures are retried. Authentication and setup errors require correction instead.

Successful matches remain visible and only failed worlds are requested again. A countdown shows pending retries. Checks continue with the window closed while logged in with sync enabled. Changing scope or pairing clears pending retries; current-character checks also clear when the character changes. Retry state is session-only.

Background watch failures no longer discard the queue of remaining worlds. Existing matches remain available, and the normal five-minute check cycle continues.

No Journal update required beyond V7.11.183 for all/current-character scopes. In-game validation remains necessary.
