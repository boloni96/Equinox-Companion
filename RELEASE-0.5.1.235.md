# Companion 0.5.1.235

- After a full game restart, **Log in as this character** can identify the current login through the game's START path and ready character list before deciding whether confirmation is needed. This initial check does not send the selected character or world to Lifestream.
- A recognized account restores locally learned service-account links. Unverified accounts still require confirmation before a target search or login; a saved Person/account label alone is not proof.
- Identification stops after 45 seconds, when cancelled, or when the target, pairing or game state changes. **Stop check** cancels Companion's request without aborting an unrelated Lifestream trip. If no paired character is available on the connected data centre, use the game's character list or log in once to identify the account.
- Dalamud Plugin Installer's **Settings** button now opens Companion directly on its **Settings** tab. The window launcher also has a Settings entry.

Validation: regression coverage for fresh-process identification, stable native-list samples, restored Main/Alt service-account links, separate logins, cancellation and expiry. CI also runs the existing regression suite, native garden text tests and plugin build before publishing. Native title-menu interaction still needs an in-game restart test.

This update changes only Companion; no Journal or Worker deployment is required.
