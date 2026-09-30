# 0.4.0.4 — resilient sync and character housing view

Plugin-only update; keep Journal V7.9.21 or newer and the existing pairing key.

- Prevent character snapshots with missing identity/world/job data during loading from entering the discovery queue.
- Validate pending records before batching; incomplete legacy records remain local and cannot block later valid actions. No events are falsely acknowledged or deleted. The Tests tab reports held records.
- Two top tabs: Tests and Characters & housing. Existing recording, export and connection controls remain under Tests.
- Housing view groups locally observed characters by identity, displays home world/data centre/region, and shows confirmed private/FC estates. Hover for estate address, size, owner/FC master, own entry and latest recorded eligible entry.
- Green for completed days 0–7; orange 8–30; red 31 through the 45-day boundary; purple after 45 days, labelled DEMOLISHED? with explicit estimated status. Unknown entries stay grey.
- Private guest entries, login observations, workshop/room visits and ordinary FC activity do not reset the estimate. Recorded members of the same confirmed FC estate can contribute qualifying entries. This is a local recorded-history estimate, not the game's authoritative countdown; other clients, membership/ownership changes, incomplete history and demolition suspensions can affect it.
- Website-only characters are not downloaded into the plugin. No new polling requests are introduced.

Validation: Release build succeeds with zero warnings/errors. Existing observation/menu/planting/chat gate tests and new sync/age/eligibility tests pass. Private 04:59 export replay holds exactly the zero-world character snapshot and retains both Vaelis owned-estate discoveries as sendable. Diagnostics stay private and are not in the package or repository. Actual in-game tab appearance and live re-enabled character sync await user testing.

Future investigation: compare FC Activity before/after an interior entry; only a verified explicit entry/demolition event should affect housing state. Login/online status is not entry evidence.
