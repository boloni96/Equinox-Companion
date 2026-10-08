# Companion 0.5.1.101
Replaces the disabled cutscene skip stub with a fresh, default-off follower permission. Records the leader's game-owned skip Yes for the exact quest scene. Requests Escape only in this client's matching cutscene, verifies AgentCutscene owns the live SelectString and has a non-null callback, then sends one full typed SelectString callback. Never synthesizes a skip dialog with a null callback or uses FireCallbackInt for skipping. Times out without retrying. English skip prompts only.
Build and policy gates validate compilation and guards; native in-game behavior remains unverified. No crash reproduction performed.

Recognizes the two observed first-time/returning Kipih greetings only in quest 68694 scene 1 with the same speaker. Other mismatches remain blocked.
