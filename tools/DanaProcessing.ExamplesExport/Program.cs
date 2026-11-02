using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DanaProcessing.Ide.Compilation.PackageManagement;
using DanaProcessing.Ide.Editor;
using DanaProcessing.Ide.Localization;

// The IDE's samples as JSON for the VS Code extension: name and description
// in English and Spanish, tags, the NuGet packages their `// nuget:` lines
// ask for, and the source.
//
// The catalog reads the UI language once, when it's first built, so each
// language is exported by a child process (`--lang en|es`) and merged here.
var json = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

if (args.Length == 2 && args[0] == "--lang")
{
    var language = args[1] == "es" ? AppLanguage.Spanish : AppLanguage.English;
    Loc.Initialize(language);
    var catalog = SketchSamples.All.Select(sample => new
    {
        name = sample.Name,
        description = sample.Description,
        tags = sample.Tags.Select(tag => new { id = tag.ToString(), label = SampleTags.Label(tag) }).ToArray(),
        packages = PackageDirectiveParser.Parse(sample.Source).Select(p => new { id = p.Id, version = p.Version }).ToArray(),
        source = sample.Source.Replace("\r\n", "\n"),
    });
    Console.Out.Write(JsonSerializer.Serialize(catalog, json));
    return 0;
}

var output = args.Length > 0 ? args[0] : "examples.json";

JsonArray Export(string lang)
{
    var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, UseShellExecute = false };
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!) == "dotnet")
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
    start.ArgumentList.Add("--lang");
    start.ArgumentList.Add(lang);
    using var child = Process.Start(start)!;
    var text = child.StandardOutput.ReadToEnd();
    child.WaitForExit();
    if (child.ExitCode != 0) throw new InvalidOperationException($"export --lang {lang} failed ({child.ExitCode})");
    return JsonNode.Parse(text)!.AsArray();
}

var en = Export("en");
var es = Export("es");
if (en.Count != es.Count) throw new InvalidOperationException("The English and Spanish catalogs differ in length.");

var merged = new JsonArray();
for (int i = 0; i < en.Count; i++)
{
    var e = en[i]!; var s = es[i]!;
    var tags = new JsonArray();
    var enTags = e["tags"]!.AsArray(); var esTags = s["tags"]!.AsArray();
    for (int t = 0; t < enTags.Count; t++)
        tags.Add(new JsonObject { ["id"] = enTags[t]!["id"]!.GetValue<string>(), ["en"] = enTags[t]!["label"]!.GetValue<string>(), ["es"] = esTags[t]!["label"]!.GetValue<string>() });
    merged.Add(new JsonObject
    {
        ["name"] = new JsonObject { ["en"] = e["name"]!.GetValue<string>(), ["es"] = s["name"]!.GetValue<string>() },
        ["description"] = new JsonObject { ["en"] = e["description"]!.GetValue<string>(), ["es"] = s["description"]!.GetValue<string>() },
        ["tags"] = tags,
        ["packages"] = e["packages"]!.DeepClone(),
        ["source"] = e["source"]!.GetValue<string>(),
    });
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, merged.ToJsonString(json) + "\n");
Console.WriteLine($"{merged.Count} examples -> {output}");
return 0;
