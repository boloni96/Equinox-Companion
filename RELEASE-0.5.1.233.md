Equinox Companion 0.5.1.233 — verify title-screen login automatically

The title-screen Log in as this character button no longer asks you to identify
the current Square Enix login before checking it. When the current login is
unknown, Companion uses Lifestream's ConnectAndOpenCharaSelect IPC to open the
character list without logging in. It verifies the selected character against
the current unlocked native list, then continues login automatically.

The check waits for Lifestream to finish, requires two ready samples, matches the
stored Content ID when available, and also checks the actual name and home world.
The ready list restores previously learned service-account links for this game
process. Separate game logins never inherit identity from saved account labels.

Checking appears beside the selected character, with Stop check. A 45-second
limit stops an absent-character search and explains the Lifestream service-account
or Square Enix login checks needed. Changed pairing, moved/removed characters,
manual login, cancellation and plugin unload stop the pending check.

Known in-game account switching retains its existing behavior and warnings.
No Journal or Background Worker deployment is required.

Verification: automated first-launch, second service account, separate login,
locked character, duplicate/stale list, timeout and cancellation scenarios are
included in the existing transition suite. The Windows release workflow also
runs the complete regression suite and native garden checks before publishing.
Actual game UI timing still needs an in-game check after installation.

Primary API references reviewed 2026-10-22:
https://github.com/NightmareXIV/Lifestream/blob/main/Lifestream/IPC/IPCProvider.cs
https://github.com/NightmareXIV/Lifestream/blob/main/Lifestream/Tasks/Login/TaskConnectAndOpenCharaSelect.cs
https://github.com/NightmareXIV/Lifestream/blob/main/Lifestream/Tasks/Login/TaskChangeCharacter.cs
https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentLobby.cs
