using System.Text.Json;
using DanaProcessing.Ide.Editor;
using Xunit;

namespace DanaProcessing.Tests.Packaging;

/// <summary>
/// The VS Code extension's examples (vscode-extension/examples.json) are the
/// IDE's samples, exported. A sample added or edited here needs the file
/// exported again:
///   dotnet run --project tools/DanaProcessing.ExamplesExport -- vscode-extension/examples.json
/// </summary>
public class ExamplesExportTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DanaProcessing.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }

    [Fact]
    public void Vscode_examples_match_the_ide_samples()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(RepoFile("vscode-extension", "examples.json")));
        var exported = json.RootElement.EnumerateArray().Select(e => e.GetProperty("source").GetString()).ToList();
        var samples = SketchSamples.All.Select(s => s.Source.Replace("\r\n", "\n")).ToList();

        Assert.True(samples.Count == exported.Count && samples.SequenceEqual(exported),
            "vscode-extension/examples.json is out of date: run " +
            "`dotnet run --project tools/DanaProcessing.ExamplesExport -- vscode-extension/examples.json`.");
    }
}
