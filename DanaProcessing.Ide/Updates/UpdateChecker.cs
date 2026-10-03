using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace DanaProcessing.Ide.Updates
{
    /// <summary>Result of a successful update check — only ever returned when Velopack's
    /// own feed genuinely has something newer than the installed version, so IsNewer is
    /// a real comparison against the actual installed app, not a guess.</summary>
    public sealed record UpdateCheckResult(bool IsNewer, string LatestVersion);

    /// <summary>
    /// Checks the GitHub Release feed Velopack publishes to (see
    /// .github/workflows/build-release.yml's vpk pack/upload steps) for a newer
    /// version than the one currently installed, and can actually download and
    /// apply it -- unlike the old GitHub-REST-API-polling version of this class,
    /// this is a REAL updater: Velopack's own installer can replace a running app
    /// in place (the old "a self-contained exe can't overwrite itself" limitation
    /// was specific to the hand-rolled zip/tar builds, not a fundamental one).
    ///
    /// Deliberately returns null instead of throwing for every failure mode --
    /// offline, GitHub down, or (the common one during local development) the
    /// app not being a real Velopack install at all (IsInstalled is false for a
    /// plain `dotnet run`/debug launch). A background version check must never
    /// crash or interrupt the app over any of that.
    /// </summary>
    public static class UpdateChecker
    {
        // Same repo UpdateChecker always pointed at. `prerelease: false` --
        // this app's own version scheme (see build-release.yml's read-version
        // step) tags every ordinary push as a prerelease-flavored version
        // ("0.1.0-beta.47"); only a deliberate, hand-bumped <Version> cut via
        // workflow_dispatch's cut_release flag should ever reach users
        // automatically, so day-to-day CI churn doesn't nag everyone who has
        // the IDE open with a brand new "update" every few minutes.
        private static readonly GithubSource Source = new(
            "https://github.com/BuddhaCodes/DanaProcessing", null, prerelease: false);

        // Cached from the most recent successful CheckAsync() call so
        // DownloadAndApplyAsync (triggered later, from the update banner's
        // "Descargar" click) doesn't need to re-check -- same UpdateManager/
        // UpdateInfo pair that already confirmed a newer version exists.
        private static UpdateManager? _manager;
        private static UpdateInfo? _pendingUpdate;

        public static async Task<UpdateCheckResult?> CheckAsync(CancellationToken ct = default)
        {
            try
            {
                var manager = new UpdateManager(Source);
                if (!manager.IsInstalled)
                    return null; // dev build / dotnet run -- nothing to update

                var newVersion = await manager.CheckForUpdatesAsync();
                if (newVersion is null)
                    return null;

                _manager = manager;
                _pendingUpdate = newVersion;
                return new UpdateCheckResult(true, newVersion.TargetFullRelease.Version.ToString());
            }
            catch (NotInstalledException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Actually downloads and applies the update CheckAsync last found,
        /// then restarts into the new version -- the real "Velopack handles updates"
        /// replacement for the old implementation's Process.Start(releaseUrl) (which
        /// only ever opened a browser tab). A no-op if there's nothing pending (e.g.
        /// called without a prior successful CheckAsync, or the process restarted
        /// since then and lost the cached state).</summary>
        public static async Task DownloadAndApplyAsync(CancellationToken ct = default)
        {
            if (_manager is not { } manager || _pendingUpdate is not { } update)
                return;

            await manager.DownloadUpdatesAsync(update, cancelToken: ct);
            manager.ApplyUpdatesAndRestart(update);
        }
    }
}
