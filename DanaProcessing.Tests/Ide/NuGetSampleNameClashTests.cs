using System.Text.RegularExpressions;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>
/// Samples that use NuGet packages can't be compiled here (the packages come
/// from nuget.org at Run), so a name the package shares with Dana -- every
/// sketch imports DanaProcessing -- would only show up as CS0104 "ambiguous
/// reference" when someone presses Run. These are the known clashes; a sample
/// that imports the package must write the package's type out in full.
/// </summary>
public class NuGetSampleNameClashTests
{
    /// <summary>Package namespace -> type names it shares with DanaProcessing.</summary>
    private static readonly Dictionary<string, string[]> Clashes = new()
    {
        ["NAudio.Midi"] = new[] { "MidiMessage" },   // DanaProcessing.MidiMessage (OnMidi)
    };

    public static IEnumerable<object[]> Cases() =>
        from sample in DanaProcessing.Ide.Editor.SketchSamples.All
        from clash in Clashes
        where Regex.IsMatch(sample.Source, @"^\s*using\s+" + Regex.Escape(clash.Key) + @"\s*;", RegexOptions.Multiline)
        select new object[] { sample.Name, clash.Key };

    [Fact]
    public void Some_sample_imports_a_clashing_namespace() => Assert.NotEmpty(Cases());

    [Theory]
    [MemberData(nameof(Cases))]
    public void Shared_names_are_written_out_in_full(string sampleName, string ns)
    {
        var source = DanaProcessing.Ide.Editor.SketchSamples.All.Single(s => s.Name == sampleName).Source;
        var code = string.Join("\n", source.Split('\n').Where(l => !l.TrimStart().StartsWith("//")));   // comments may say the name
        foreach (var name in Clashes[ns])
            Assert.False(Regex.IsMatch(code, @"(?<![\w.])" + name + @"\b"),
                $"{sampleName}: '{name}' is both {ns}.{name} and DanaProcessing.{name}; write {ns}.{name}");
    }
}
