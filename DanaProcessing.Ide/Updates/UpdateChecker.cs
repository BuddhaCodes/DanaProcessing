using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DanaProcessing.Ide;
using NuGet.Versioning;

namespace DanaProcessing.Ide.Updates
{
    /// <summary>Result of a successful update check — only ever returned when a newer
    /// release's tag actually parsed as a version, so IsNewer is a real comparison,
    /// not a guess.</summary>
    public sealed record UpdateCheckResult(bool IsNewer, string LatestVersion, string ReleaseUrl);

    /// <summary>
    /// Checks GitHub's own Releases API for a newer build than the one currently
    /// running — notify-only, nothing here ever downloads or replaces the app
    /// itself (see ROADMAP.md-adjacent reasoning: a single-file self-contained
    /// exe can't overwrite itself while running on Windows, and a first cut isn't
    /// the place to take on that risk).
    ///
    /// Deliberately returns null instead of throwing for every failure mode —
    /// offline, GitHub down, rate-limited, or (very real today, see below) a
    /// release tag that isn't a version at all. A background version check must
    /// never crash or interrupt the app over any of that.
    /// </summary>
    public static class UpdateChecker
    {
        private const string ReleasesApiUrl = "https://api.github.com/repos/BuddhaCodes/DanaProcessing/releases/latest";

        public static async Task<UpdateCheckResult?> CheckAsync(CancellationToken ct = default)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                // GitHub's REST API rejects any request with no User-Agent (403) —
                // confirmed this is a real requirement, not a maybe.
                http.DefaultRequestHeaders.UserAgent.ParseAdd("DanaProcessingIde-UpdateChecker");

                using var response = await http.GetAsync(ReleasesApiUrl, ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                if (!doc.RootElement.TryGetProperty("tag_name", out var tagProp) || tagProp.GetString() is not { } tagName)
                    return null;
                if (!doc.RootElement.TryGetProperty("html_url", out var urlProp) || urlProp.GetString() is not { } releaseUrl)
                    return null;

                // Today's actual latest release is tagged "build-13" (a leftover
                // from before this project switched to tagging releases as
                // "v{Version}") — TryParse failing here is the normal, expected
                // "nothing to compare yet" case for any repo whose tags don't all
                // follow semver, not a bug to work around.
                if (!NuGetVersion.TryParse(tagName.TrimStart('v', 'V'), out var remoteVersion))
                    return null;
                if (!NuGetVersion.TryParse(AppVersion.Current, out var currentVersion))
                    return null;

                return new UpdateCheckResult(remoteVersion > currentVersion, remoteVersion.ToString(), releaseUrl);
            }
            catch
            {
                return null;
            }
        }
    }
}
