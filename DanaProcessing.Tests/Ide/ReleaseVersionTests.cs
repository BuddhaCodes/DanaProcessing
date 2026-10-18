using System.Diagnostics;
using NuGet.Versioning;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// .github/scripts/ci-version.sh, which build-release.yml uses to version each
/// run. Velopack refuses to pack a version that isn't greater than the one it
/// downloaded ("There is a release in channel linux which is equal or greater"),
/// which is what broke when the csproj went from 1.0.0-rc.1 to 1.0.0: CI made
/// 1.0.0-ci.49, and "ci" sorts before "rc". Checked with NuGet.Versioning, the
/// semver Velopack compares with.
/// </summary>
public class ReleaseVersionTests
{
    private static (string Version, bool Pre)? Run(string csproj, int run, bool cut, bool? released = null)
    {
        var root = DocsExamplesTests.FindSiteRoot();
        var script = root == null ? null : Path.Combine(root, ".github", "scripts", "ci-version.sh");
        if (script == null || !File.Exists(script))
            return null;
        var psi = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add(csproj);
        psi.ArgumentList.Add(run.ToString());
        psi.ArgumentList.Add(cut ? "true" : "false");
        psi.Environment["RELEASED"] = released switch { true => "true", false => "false", _ => "" };
        Process? p;
        try { p = Process.Start(psi); }
        catch (System.ComponentModel.Win32Exception) { return null; } // no bash on this machine
        if (p == null) return null;
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        Assert.Equal(0, p.ExitCode);
        var values = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim().Split('=', 2)).ToDictionary(kv => kv[0], kv => kv[1]);
        return (values["version"], values["prerelease"] == "true");
    }

    [Fact]
    public void Every_build_is_newer_than_the_last_one_published_and_older_than_the_release_it_leads_to()
    {
        var steps = new[]
        {
            Run("1.0.0-rc.1", 47, cut: false),
            Run("1.0.0-rc.1", 48, cut: false),
            Run("1.0.0", 49, cut: false, released: false),  // the run that failed with 1.0.0-ci.49
            Run("1.0.0", 50, cut: false, released: false),
            Run("1.0.0", 51, cut: true),                    // the official release
            Run("1.0.0", 52, cut: false, released: true),   // pushes after it: toward 1.0.1
            Run("1.0.1", 53, cut: true),
        };
        if (steps.Any(s => s == null))
            return; // no bash here (CI on every OS has it)

        var versions = steps.Select(s => SemanticVersion.Parse(s!.Value.Version)).ToList();
        for (int i = 1; i < versions.Count; i++)
            Assert.True(versions[i] > versions[i - 1], $"{versions[i]} must be newer than {versions[i - 1]}");

        Assert.Equal("1.0.0-rtm.49", steps[2]!.Value.Version);
        Assert.Equal("1.0.0", steps[4]!.Value.Version);
        Assert.Equal("1.0.1-ci.52", steps[5]!.Value.Version);
        Assert.False(SemanticVersion.Parse("1.0.0-ci.49") > SemanticVersion.Parse("1.0.0-rc.1.48")); // why the old scheme broke
    }

    [Fact]
    public void Only_a_cut_release_is_stable_so_ci_builds_never_reach_users_as_updates()
    {
        var ci = Run("1.0.0", 60, cut: false, released: false);
        var cut = Run("1.0.0", 61, cut: true);
        if (ci == null || cut == null)
            return;
        Assert.True(ci.Value.Pre);
        Assert.False(cut.Value.Pre);
    }

    [Fact]
    public void The_workflow_uses_the_script_and_uploads_ci_builds_as_pre_releases()
    {
        var root = DocsExamplesTests.FindSiteRoot();
        if (root == null) return;
        var yml = File.ReadAllText(Path.Combine(root, ".github", "workflows", "build-release.yml"));
        Assert.Contains(".github/scripts/ci-version.sh", yml);
        Assert.Contains("prerelease: ${{ steps.version.outputs.prerelease }}", yml);
        Assert.Contains("needs.read-version.outputs.prerelease == 'true' && '--pre'", yml);
    }
}
