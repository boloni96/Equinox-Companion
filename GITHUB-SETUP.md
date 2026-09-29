# GitHub custom repository setup

Prepared for `boloni96/Equinox-Companion`. The source layout is prepared for this repository. The install URL becomes
available after the first successful Publish Dalamud plugin workflow run.

## One-time setup

1. Create a **public** GitHub repository named `Equinox-Companion` under
   `boloni96`. Initialize it with a README so it has a default branch.
2. Grant the connected GitHub app access to this new repository if the app is
   restricted to selected repositories. The assistant can then upload these
   prepared files using the connected GitHub tools.
3. Put this package's contents at the repository root, including `.github`.
   Do not upload the ZIP itself as a substitute for extracting its contents.
4. Open **Actions → Publish Dalamud plugin → Run workflow** on the default
   branch. This builds and tests the plugin, creates a draft with its files,
   and then publishes the completed release.

No personal access token, Cloudflare credential or journal password is needed
by this workflow. It uses GitHub's built-in repository-scoped workflow token.
Only plugin source/binaries and documentation belong here; do not add journal
backups, personal pictures or exported diagnostic sessions.

## Add the link in Dalamud after the first successful release

Open `/xlsettings` → **Experimental → Custom Plugin Repositories**.
Add this link and enable its checkbox, then save:

```text
https://github.com/boloni96/Equinox-Companion/releases/latest/download/repo.json
```

Open `/xlplugins`, find **Equinox Companion (Test)**, and install it. This is
a Dalamud repository link; Penumbra's mod settings are not used.

If the development DLL was loaded before, disable/remove its Dev Plugin
Locations entry before installing the repository copy. Keep your configuration.

## Updates

Change the source, increase `<Version>` in `src/EquinoxCompanion.csproj`, update
`RELEASE-NOTES.md`, and run the workflow again. The stable repository link points
to the newest published release's `repo.json`; that file points to the exact
versioned plugin ZIP. Dalamud then offers the higher assembly version through
its normal plugin updater. Users keep the same custom-repository link.

Do not overwrite an existing release. If an upload failed and left a draft,
inspect/remove that incomplete draft before retrying the same version. A newer
Dalamud API may require updating the SDK, code and packaging API check first.

The package generator was tested locally against the compiled 0.1.0.0 build.
The GitHub Actions workflow has not yet run in GitHub.

Source/workflow changes pushed to main also start the release workflow.
