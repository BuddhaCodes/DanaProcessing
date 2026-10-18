# Releasing DanaProcessing

Installers are built by `.github/workflows/build-release.yml` with
[Velopack](https://velopack.io): one per OS (Windows `Setup.exe`, macOS
`.app` for Intel and Apple Silicon, Linux AppImage), plus the update feed the
IDE checks on startup.

## Every push to `master`

Builds a GitHub **pre-release** with a unique, always-increasing version
(`.github/scripts/ci-version.sh`). Nothing to do by hand.

| `<Version>` in the csproj | CI builds are | Why |
|---|---|---|
| `1.0.0-rc.1` | `1.0.0-rc.1.<run>` | |
| `1.0.0`, not released yet | `1.0.0-rtm.<run>` | "rtm" sorts after "rc" and before `1.0.0` |
| `1.0.0`, `v1.0.0` already released | `1.0.1-ci.<run>` | builds toward the next patch |

Each one must be newer than the release `vpk pack` downloads for the delta,
or it stops with *"There is a release in channel … which is equal or greater"*
(that's what happened when `1.0.0-ci.49` came after `1.0.0-rc.1.48`: "ci"
sorts before "rc"). Because they're pre-releases, the IDE's updater never
offers them and the website's download button never points at them: only a
cut release reaches users.

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

## Checking export on every OS

Every CI run uploads an artifact per OS called `exports-<os>`: a GIF, an
MP4, an MP4 with sound and a YouTube thumbnail, all rendered on that OS by
`ReelPolishTests.Renders_every_export_format_for_inspection`. Before a
release, download the three and check:

- the GIF loops and has the right size (270 px wide);
- both MP4s play in a normal player, and `reel-with-sound.mp4` has a beat
  on every second;
- the thumbnail is 1280x720 with the title on the left and the sketch on
  the right;
- the three OSes look the same (fonts may differ slightly).

The export tests (GIF frame count, PNG sequence, MP4 with an audio track)
run on all three OSes in CI; ffmpeg is installed there for the MP4 ones.

## Before tagging 1.0

- [ ] CI green on Windows, macOS and Linux.
- [ ] `exports-<os>` artifacts checked as above.
- [ ] Sound tried at least once on a real Mac and a real Linux desktop (or
      the README keeps saying "experimental").
- [ ] Installer + update tested on Windows (previous version installed).
- [ ] `<Version>` set to `1.0.0` in the three project files.

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
