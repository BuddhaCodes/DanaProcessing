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
}
