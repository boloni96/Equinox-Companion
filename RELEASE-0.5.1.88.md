# Companion 0.5.1.88 / Journal V7.11.86

Fix the .87 travel timing regression: the relay accepts original click timestamps throughout the existing two-minute lifetime, without refreshing expiry. A 15-second cast/loading delay no longer fails the previous ten-second validator. Pause cutoffs continue to reject ordinary trips selected before Resume.

Bring follower back uses the server's acknowledged Resume token for its one intended session. The server requires that exact still-active Resume and stamps the meeting request after it. This removes the cross-clock comparison that could silently discard a fresh Bring request; a later Pause/Stop invalidates it. No stopped follower can be restarted by this request.

Read bounded relay acknowledgements: HTTP success with zero recipients is now explicitly not queued. Positive recipients mean queued, not received or arrived. Diagnostics record submission results, actual follower receipt and local session discard reasons.

Both .87 reports confirmed Pause stopped movement. The leader's 00:51 teleport spent almost ten seconds before submission and then showed HTTP400; later Bring submissions produced no follower receipt records. The old logs did not include server rejection reasons, so the precise cause of each missing Bring cannot be proved retrospectively. Tests reproduce delayed-load rejection and clock-skewed Bring loss. Native retesting remains required.
