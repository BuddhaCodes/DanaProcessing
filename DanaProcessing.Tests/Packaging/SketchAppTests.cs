using System.Reflection;
using System.Reflection.Emit;
using DanaProcessing.AvaloniaHost;
using Xunit;

namespace DanaProcessing.Tests.Packaging;

/// <summary>SketchApp: what the NuGet package runs a sketch with, outside the IDE.</summary>
public class SketchAppTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dana_app_" + Guid.NewGuid().ToString("N"));

    public SketchAppTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Folder(string name, bool withData)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(withData ? Path.Combine(path, "data") : path);
        return path;
    }

    [Fact]
    public void Sketch_folder_is_the_working_directory_with_dotnet_run()
    {
        var project = Folder("project", withData: true);
        var bin = Folder("bin", withData: true);
        Assert.Equal(project, SketchApp.SketchFolderFor(project, bin));
    }

    [Fact]
    public void Sketch_folder_is_the_program_folder_when_only_it_has_data()
    {
        var elsewhere = Folder("desktop", withData: false);
        var app = Folder("app", withData: true);
        Assert.Equal(app, SketchApp.SketchFolderFor(elsewhere, app));
    }

    [Fact]
    public void Sketch_folder_stays_the_working_directory_when_nobody_has_data()
    {
        var cwd = Folder("cwd", withData: false);
        var app = Folder("app2", withData: false);
        Assert.Equal(cwd, SketchApp.SketchFolderFor(cwd, app));
    }

    // The emitted test sketches derive from these (they're in this test
    // assembly, which FindSketchType isn't asked about).
    public class OnlySketch : Sketch { public override void Draw() { } }
    public abstract class AbstractSketch : Sketch { public override void Draw() { } }

    [Fact]
    public void Finds_the_one_sketch_and_ignores_abstract_ones()
    {
        var found = SketchApp.FindSketchType(Emit(typeof(OnlySketch), typeof(AbstractSketch)));
        Assert.Equal(typeof(OnlySketch), found.BaseType);
    }

    [Fact]
    public void Several_sketches_ask_which_one_to_run()
    {
        var error = Assert.Throws<InvalidOperationException>(() => SketchApp.FindSketchType(Emit(typeof(OnlySketch), typeof(OnlySketch))));
        Assert.Contains("SketchApp.Run(new MySketch())", error.Message);
    }

    [Fact]
    public void No_sketch_says_how_to_add_one()
    {
        var error = Assert.Throws<InvalidOperationException>(() => SketchApp.FindSketchType(Emit()));
        Assert.Contains("inherits from Sketch", error.Message);
    }

    /// <summary>A throwaway assembly holding one new subclass per base type given (abstract if its base is).</summary>
    private static Assembly Emit(params Type[] bases)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Sketches" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("m");
        for (int i = 0; i < bases.Length; i++)
        {
            var type = module.DefineType("S" + i, TypeAttributes.Public | (bases[i].IsAbstract ? TypeAttributes.Abstract : 0), bases[i]);
            type.DefineDefaultConstructor(MethodAttributes.Public);
            type.CreateType();
        }
        return assembly;
    }
}
