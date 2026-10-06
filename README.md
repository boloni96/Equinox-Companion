# Equinox Companion 0.5.1.50

Current update: My Empire, /gardening batch following, coloured garden groups,
QuickLoot filter/feedback fixes and indexed garden-history lookup. See RELEASE-NOTES.md.
Use the existing repo.json installer feed. Journal remains V7.11.66.

# Equinox Companion 0.5.1.2 — validation build

Read RELEASE-NOTES.md and WORK-STATUS.md. This update uses the existing GitHub installer feed and requires Journal V7.11.1 for background housing propagation. The 23-point implementation and live-validation boundaries are recorded in WORK-STATUS.md.

# Equinox Companion

Prepared installer build: **0.5.1.44**, for **Journal V7.11.55**. Check `repo.json` for the published version. This release needs in-game validation; the complete feature audit and remaining gaps are in [WORK-STATUS.md](WORK-STATUS.md).

Add this repository in Dalamud → Settings → Experimental:

```
https://raw.githubusercontent.com/boloni96/Equinox-Companion/main/repo.json
```

Update/install Equinox Companion and open `/equinox`. Deploy the website package to the existing Cloudflare Pages project first, then let it save once. Keep the same pairing key for both users. Tracking and enabled uploads continue with the plugin window closed; known-house entries and names can propagate between paired plugins with Journal V7.11.1 closed after its first save with automatic application enabled. The full journal and gardens still apply events when the website is open.

The plugin observes normal game actions and loaded data. It does not perform game actions. English garden menus are supported. A matched crop reward clears the exact harvested bed. If it could not be matched, reopen the empty bed to refresh its state.

See [RELEASE-NOTES.md](RELEASE-NOTES.md) for setup, changes, limitations and live tests. Build with .NET 10 and official Dalamud API 15 references; run `dotnet run --project tests/GateTests.csproj -c Release`, then `dotnet build src/EquinoxCompanion.csproj -c Release`. Package with `scripts/package_release.py`. Prepared releases are checksum-verified before GitHub Actions publishes the package and installer feeds together.

Private journal exports, pairing keys and diagnostic recordings must not be committed to this repository.

Company Profile support requires Journal V7.11.11. Deploy and save the website first, then refresh shared profiles in Companion.


### Fashion Report picture window
Use `/fashionr` to open the current V1 picture inside the game. Click the picture to expand or shrink it. A clickable browser link is also printed in local chat. Refresh downloads the current image; Open in browser is optional. `/equinox fashion` works if another plugin owns `/fashionr`. The shortcut never marks the task complete. Downloads happen asynchronously on demand, with a shared in-memory picture checked at each character login and hourly while logged in, including when the picture window is closed. Closing the window does not interrupt character sync.

## Optional QuickLoot

Disabled by default. Enable in Settings > Features to show the QuickLoot tab.
Shared settings, nested rolling/filter/rule/feedback sections, top-bar controls
and LazyLoot credits. See RELEASE-NOTES-0.5.1.49.md for behavior and live checks.

## Optional local FollowThem and Treasure Coffer markers

Companion 0.5.1.54 adds both under Settings > Features, disabled by default. Settings stay local; coffer state is not uploaded. FollowThem has party/friend selection, a top-bar start/stop control and optional wait/resume. No vnavmesh. Teleport acceptance requires an open English offer; optional portal relay requires Journal V7.11.74 and shares only confirmed recent transitions. Matching supported confirmations can be accepted; destination lists and other portal types remain manual. Coffer red/green markers use native minimap layout and confirmed opening state for the current visit. Native game validation remains pending. See RELEASE-0.5.1.54.md and checklist tests 251–263.

## Companion 0.5.1.55

FollowThem chat status messages (optional), guarded duty-entry movement commands,
and single coloured coffer icons. Choose minimap/main-map displays independently
under Settings > Features. Map alignment and duty entry require live testing.
Journal remains V7.11.74; no new website deployment for this update.

## Companion 0.5.1.56

Six local coffer appearances with clickable red/green previews: Game chest, Classic,
Rounded, Royal jewel, Pixel, and Minimal outline. Choose under Settings > Features
> Treasure Coffer markers > Coffer appearance. Applies to both maps. Custom styles
hide the underlying minimap symbol and restore it when switching back/disabling.
Native drawing and cleanup still require in-game testing. Journal stays V7.11.74.
