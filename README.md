# Equinox Companion

Dalamud API 15 plugin for local house-entry and gardening observations. Current version: 0.3.0.0.

Add this custom repository URL in Dalamud's Experimental settings:

https://raw.githubusercontent.com/boloni96/Equinox-Companion/main/repo.json

Install or update Equinox Companion, then open `/equinox`.
Enable **Automatically save confirmed tending locally** to record normal tending without starting a test. The plugin observes your actions; it does not perform them. English garden menu titles are currently supported. House visits are recorded independently of this toggle.

Successful tending records are saved locally by character, house, patch and bed. The house view shows detected entries, with startup-inside observations labeled separately. Optional pairing to Journal V7.9.14 sends confirmed house entries and tending. Open Website connection in /equinox; create a key from Game connection on the website. Characters match by name and home server; houses by full address; physical patches need a one-time link. Optional five-minute recordings export diagnostics for troubleshooting; exports contain character identities and property addresses and should stay private.

See ROADMAP.md for the deferred in-game planner and website integration requirements. Build locally with .NET 10 and Dalamud API 15 references; package using scripts/package_release.py. GitHub Actions is manual-only.
