# Equinox Companion 0.5.1.2 — validation build

Read RELEASE-NOTES.md and WORK-STATUS.md. This update uses the existing GitHub installer feed and requires Journal V7.11.1 for background housing propagation. The 23-point implementation and live-validation boundaries are recorded in WORK-STATUS.md.

# Equinox Companion

Prepared installer build: **0.5.1.2**, for **Journal V7.11.1**. Check `repo.json` for the published version. This release needs in-game validation; the complete feature audit and remaining gaps are in [WORK-STATUS.md](WORK-STATUS.md).

Add this repository in Dalamud → Settings → Experimental:

```
https://raw.githubusercontent.com/boloni96/Equinox-Companion/main/repo.json
```

Update/install Equinox Companion and open `/equinox`. Deploy the website package to the existing Cloudflare Pages project first, then let it save once. Keep the same pairing key for both users. Tracking and enabled uploads continue with the plugin window closed; known-house entries and names can propagate between paired plugins with Journal V7.11.1 closed after its first save with automatic application enabled. The full journal and gardens still apply events when the website is open.

The plugin observes normal game actions and loaded data. It does not perform game actions. English garden menus are supported. After harvesting, reopen the numbered bed menu to synchronize its empty state.

See [RELEASE-NOTES.md](RELEASE-NOTES.md) for setup, changes, limitations and live tests. Build with .NET 10 and official Dalamud API 15 references; run `dotnet run --project tests/GateTests.csproj -c Release`, then `dotnet build src/EquinoxCompanion.csproj -c Release`. Package with `scripts/package_release.py`. The existing GitHub Actions workflow remains manual-only.

Private journal exports, pairing keys and diagnostic recordings must not be committed to this repository.

Company Profile support requires Journal V7.11.11. Deploy and save the website first, then refresh shared profiles in Companion.


### Fashion Report picture window
Use `/fashion` to open the current V1 picture inside the game. Click the picture to expand or shrink it. A clickable browser link is also printed in local chat. Refresh downloads the current image; Open in browser is optional. `/equinox fashion` works if another plugin owns `/fashion`. The shortcut never marks the task complete. Downloads happen asynchronously on demand, with an in-memory image reused for 15 minutes and automatic refresh every 15 minutes while the picture window is open. Closing the window does not interrupt character sync.
