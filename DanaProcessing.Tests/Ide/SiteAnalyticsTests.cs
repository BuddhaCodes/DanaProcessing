using System.Text.RegularExpressions;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// The website asks before measuring: no page loads Google's tag by itself;
/// site.js loads it only after "Yes, count my visit", and every page has the
/// footer button to change that answer. (The browser behaviour -- banner,
/// remembered answer, Global Privacy Control, nothing without JavaScript -- is
/// checked against the real pages in Chromium; this guards the markup.)
/// </summary>
public class SiteAnalyticsTests
{
    [Fact]
    public void No_page_loads_analytics_without_asking()
    {
        var root = DocsExamplesTests.FindSiteRoot();
        if (root == null)
            return; // not in a checkout

        foreach (var (path, html) in DocsExamplesTests.Pages(root))
        {
            Assert.DoesNotContain("googletagmanager.com", html);
            Assert.DoesNotMatch(new Regex(@"gtag\s*\("), html);
            Assert.Matches(new Regex("<meta name=\"dana-analytics\" content=\"G-[A-Z0-9]+\">"), html);
            Assert.True(html.Contains("data-privacy-choices"), $"{path}: no 'Privacy choices' button in the footer");
        }
    }

    [Fact]
    public void The_tag_is_only_loaded_from_the_answer_to_the_banner()
    {
        var root = DocsExamplesTests.FindSiteRoot();
        if (root == null)
            return;

        var js = File.ReadAllText(Path.Combine(root, "assets", "site.js"));
        Assert.Contains("Yes, count my visit", js);
        Assert.Contains("No thanks", js);
        Assert.Contains("globalPrivacyControl", js);
        // The only place the script is added: inside loadAnalytics, which runs on "granted".
        Assert.Single(Regex.Matches(js, "googletagmanager\\.com"));
        Assert.Matches(new Regex(@"if \(consent === ""granted""\) loadAnalytics\(\)"), js);
    }
}
