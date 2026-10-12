# Releasing DanaProcessing

Installers are built by `.github/workflows/build-release.yml` with
[Velopack](https://velopack.io): one per OS (Windows `Setup.exe`, macOS
`.app` for Intel and Apple Silicon, Linux AppImage), plus the update feed the
IDE checks on startup.

## Every push to `master`

Builds a pre-release with a unique version (`<Version>-ci.<run>`), so the
update feed always moves forward. Nothing to do by hand.

## Cutting a real release (e.g. 1.0.0)

1. Make sure **CI is green** on all three OSes (`.github/workflows/ci.yml`).
2. Set `<Version>` to the release number in the three project files:
   `DanaProcessing/DanaProcessing.csproj`,
   `DanaProcessing.AvaloniaHost/DanaProcessing.AvaloniaHost.csproj`,
   `DanaProcessing.Ide/DanaProcessing.Ide.csproj`.
3. Update `ROADMAP.md` / the changelog, commit, merge to `master`.
4. In GitHub → Actions → *Build and Release* → **Run workflow** with
   **cut_release = true**. The release is tagged `v<Version>` exactly.
5. Check the release page: `Setup.exe`, the two macOS zips, the AppImage and
   the `releases.*.json` feeds must all be there.
6. **Test the update path**: on a machine with the previous version
   installed, start the IDE and confirm it offers and applies the update.

## Unsigned builds (1.0)

The 1.0 installers are not code-signed (no budget yet). What users see:

- **Windows**: SmartScreen says "Windows protected your PC" →
  *More info* → *Run anyway*.
- **macOS**: Gatekeeper refuses to open it the first time →
  right-click the app → *Open* → *Open* (or
  `xattr -dr com.apple.quarantine "/Applications/DanaProcessing IDE.app"`).
- **Linux**: `chmod +x` the AppImage if your browser dropped the flag.

The workflow is ready for signing later: `vpk pack` accepts
`--signParams` (Windows) and `--signAppIdentity` / `--notaryProfile` (macOS).
SignPath offers free signing for open-source projects.
