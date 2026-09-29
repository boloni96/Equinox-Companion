# GitHub distribution

The working install feed is:

```text
https://raw.githubusercontent.com/boloni96/Equinox-Companion/main/repo.json
```

Paste it in Dalamud Settings → Experimental → Custom Plugin Repositories,
enable it, save, then install Equinox Companion (Test) from /xlplugins.

Version 0.1.0.0 is a locally compiled and tested build committed under
`dist/v0.1.0.0/EquinoxCompanion.zip`. The package contains the plugin DLL and
manifest at its root. It contains no journal backup or credentials.

GitHub Actions could not start the first build because GitHub reported an
account billing lock. No successful GitHub build is claimed. Installation from
the repository uses the existing compiled build and does not require Actions.

For later updates, build and test the new version, upload it under a new
versioned dist path, and update root repo.json with its version and download
URLs. Users keep the same repository link and use Dalamud's plugin updater.

Once the account issue is resolved, the optional manual Publish Dalamud plugin
workflow can build a release and update this same root repo.json to point at
the versioned GitHub Release asset. Increment the csproj Version first. Do not
publish a different binary using the same version number. The first blocked
run created no release. Native gardening and housing behaviour still needs
in-game verification; website sync is not enabled.
