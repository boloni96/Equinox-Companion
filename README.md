# Equinox Companion 0.5.1.1 — validation build

Read RELEASE-NOTES.md and WORK-STATUS.md. This update uses the existing GitHub installer feed and requires Journal V7.11.1 for background housing propagation. The 23-point implementation and live-validation boundaries are recorded in WORK-STATUS.md.

# Equinox Companion

Prepared installer build: **0.5.1.1**, for **Journal V7.11.1**. Check `repo.json` for the published version. This release needs in-game validation; the complete feature audit and remaining gaps are in [WORK-STATUS.md](WORK-STATUS.md).

Add this repository in Dalamud → Settings → Experimental:

```
https://raw.githubusercontent.com/boloni96/Equinox-Companion/main/repo.json
```

Update/install Equinox Companion and open `/equinox`. Deploy the website package to the existing Cloudflare Pages project first, then let it save once. Keep the same pairing key for both users. Tracking and enabled uploads continue with the plugin window closed; known-house entries and names can propagate between paired plugins with Journal V7.11.1 closed after its first save with automatic application enabled. The full journal and gardens still apply events when the website is open.

The plugin observes normal game actions and loaded data. It does not perform game actions. English garden menus are supported. After harvesting, reopen the numbered bed menu to synchronize its empty state.

See [RELEASE-NOTES.md](RELEASE-NOTES.md) for setup, changes, limitations and live tests. Build with .NET 10 and official Dalamud API 15 references; run `dotnet run --project tests/GateTests.csproj -c Release`, then `dotnet build src/EquinoxCompanion.csproj -c Release`. Package with `scripts/package_release.py`. The existing GitHub Actions workflow remains manual-only.

Private journal exports, pairing keys and diagnostic recordings must not be committed to this repository.
