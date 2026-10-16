using System.Net;
using System.Text.RegularExpressions;
using DanaProcessing.Ide.Compilation;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// Every code block on the website with a "Test in Dana" button (the
/// tutorial, the Processing guide, each section's runnable example) must
/// compile, so the docs can't drift away from the API.
/// </summary>
public class DocsExamplesTests
{
    /// <summary>The repo's index.html, found by walking up from the test binaries; null outside a checkout.</summary>
    internal static string? FindIndexHtml()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "index.html");
            if (File.Exists(path) && File.ReadAllText(path).Contains("dana-test-btn"))
                return path;
        }
        return null;
    }

    /// <summary>(id, source) of every pre that a "Test in Dana" button runs, with the highlighting stripped -- what the button sends to the IDE.</summary>
    internal static List<(string Id, string Code)> Extract(string html)
    {
        var result = new List<(string, string)>();
        foreach (Match b in Regex.Matches(html, "<button class=\"dana-test-btn\" data-target=\"([^\"]+)\""))
        {
            string id = b.Groups[1].Value;
            var pre = Regex.Match(html, "<pre[^>]*\\bid=\"" + Regex.Escape(id) + "\"[^>]*>(.*?)</pre>", RegexOptions.Singleline);
            Assert.True(pre.Success, $"button for '{id}' has no <pre id=\"{id}\">");
            result.Add((id, WebUtility.HtmlDecode(Regex.Replace(pre.Groups[1].Value, "<[^>]+>", ""))));
        }
        return result;
    }

    [Fact]
    public void The_docs_have_a_runnable_example_per_section()
    {
        var path = FindIndexHtml();
        if (path == null)
            return; // not running from a checkout
        var examples = Extract(File.ReadAllText(path));
        Assert.True(examples.Count >= 28, $"only {examples.Count} runnable examples");
        Assert.Contains(examples, e => e.Id == "tutorial-6-code");
        Assert.Contains(examples, e => e.Id == "from-processing-code");
        Assert.All(examples, e => Assert.Contains(": Sketch", e.Code));
        Assert.Equal(examples.Count, examples.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void Docs_examples_compile()
    {
        var path = FindIndexHtml();
        if (path == null)
            return;
        var failures = new List<string>();
        foreach (var (id, code) in Extract(File.ReadAllText(path)))
        {
            var r = SketchCompiler.Compile(code);
            if (!r.Success)
                failures.Add(id + ": " + string.Join(" | ", r.Errors.Take(3)));
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
    [Fact]
    public void The_live_generative_and_day_to_day_sections_have_examples()
    {
        var path = FindIndexHtml();
        if (path == null)
            return;
        var html = File.ReadAllText(path);
        foreach (var section in new[] { "live", "generative", "workflow" })
            Assert.Contains($"<section class=\"doc-section\" id=\"{section}\"", html);
        var ids = Extract(html).Select(e => e.Id).ToHashSet();
        foreach (var id in new[] { "live-example-code", "midi-example-code", "generative-example-code",
                                   "plotter-example-code", "workflow-example-code", "stats-example-code" })
            Assert.Contains(id, ids);
        // Each new API has an entry of its own.
        foreach (var entry in new[] { "midi", "beat", "listento", "performance-mode", "seed", "variations",
                                      "print-export", "stats", "data-folder", "rewind", "time-machine" })
            Assert.Contains($"<h3 class=\"entry-title\" id=\"{entry}\"", html);
    }

    [Fact]
    public void Every_keyword_and_link_points_at_something_on_the_page()
    {
        var path = FindIndexHtml();
        if (path == null)
            return;
        var html = File.ReadAllText(path);
        var ids = Regex.Matches(html, "\\bid=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();

        var map = html.Split("var keywordMap = {")[1].Split("};")[0];
        var targets = Regex.Matches(map, "\"([^\"]+)\":\\s*\"([^\"]+)\"").Select(m => (Key: m.Groups[1].Value, Id: m.Groups[2].Value)).ToList();
        Assert.Contains(targets, t => t.Key == "MidiCC");
        Assert.Contains(targets, t => t.Key == "Beat");
        Assert.All(targets, t => Assert.True(ids.Contains(t.Id), $"keyword {t.Key} -> #{t.Id} doesn't exist"));

        var links = Regex.Matches(html, "href=\"#([A-Za-z][^\"]*)\"").Select(m => m.Groups[1].Value).Distinct();
        Assert.All(links, l => Assert.True(ids.Contains(l), $"link #{l} doesn't exist"));
    }
}
