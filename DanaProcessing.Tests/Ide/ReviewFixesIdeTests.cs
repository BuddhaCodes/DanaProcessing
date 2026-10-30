using DanaProcessing.Ide.Agent;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Compilation.PackageManagement;
using NuGet.Versioning;
using Xunit;

namespace DanaProcessing.Tests.Ide;

/// <summary>A sketch that compiles but can't be constructed reports an error instead of throwing out of Compile().</summary>
public class SketchConstructorFailureTests
{
    [Fact]
    public void Field_initializer_that_throws_becomes_a_compile_error_with_its_line()
    {
        const string source = """
            public class Boom : Sketch
            {
                static int n = -1;
                int[] data = new int[n];
                public override void Setup() => Size(100, 100);
                public override void Draw() { }
            }
            """;

        var result = SketchCompiler.Compile(source);

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Contains("OverflowException", error);
        Assert.Contains("Boom", error);
        Assert.StartsWith(SketchCompiler.SketchFileName + "(4,", error);
    }

    [Fact]
    public void Constructor_that_throws_is_reported_too()
    {
        const string source = """
            public class Boom : Sketch
            {
                public Boom() { throw new InvalidOperationException("nope"); }
                public override void Draw() { }
            }
            """;

        var result = SketchCompiler.Compile(source);

        Assert.False(result.Success);
        Assert.Contains("nope", Assert.Single(result.Errors));
    }

    [Fact]
    public void A_healthy_sketch_still_compiles()
    {
        var result = SketchCompiler.Compile("public class Ok : Sketch { public override void Draw() { } }");
        Assert.True(result.Success);
    }
}

/// <summary>An interrupted agent turn must not leave a tool call without a result (both APIs reject that forever after).</summary>
public class AgentDanglingToolCallTests
{
    private static AgentMessage User(string text) => new(AgentRole.User, text, null, null);
    private static AgentMessage Calls(params string[] ids) =>
        new(AgentRole.Assistant, null, ids.Select(id => new AgentToolCall(id, "run_sketch", "{}")).ToList(), null);
    private static AgentMessage Results(params string[] ids) =>
        new(AgentRole.ToolResult, null, null, ids.Select(id => new AgentToolResult(id, "ok")).ToList());

    [Fact]
    public void Unanswered_call_gets_a_cancelled_result_right_after_it()
    {
        var messages = new List<AgentMessage> { User("hola"), Calls("a"), User("otra cosa") };

        AgentSession.RepairDanglingToolCalls(messages);

        Assert.Equal(4, messages.Count);
        Assert.Equal(AgentRole.ToolResult, messages[2].Role);
        var result = Assert.Single(messages[2].ToolResults!);
        Assert.Equal("a", result.ToolCallId);
        Assert.Contains("Cancelado", result.ResultText);
        Assert.Equal("otra cosa", messages[3].Text); // the new message stays after the repair
    }

    [Fact]
    public void Partially_answered_calls_get_only_the_missing_results()
    {
        var messages = new List<AgentMessage> { User("x"), Calls("a", "b"), Results("a") };

        AgentSession.RepairDanglingToolCalls(messages);

        Assert.Equal(3, messages.Count);
        Assert.Equal(new[] { "a", "b" }, messages[2].ToolResults!.Select(r => r.ToolCallId));
        Assert.Equal("ok", messages[2].ToolResults![0].ResultText);
    }

    [Fact]
    public void A_complete_conversation_is_left_alone()
    {
        var messages = new List<AgentMessage> { User("x"), Calls("a"), Results("a"), new(AgentRole.Assistant, "listo", null, null) };
        var before = messages.ToList();

        AgentSession.RepairDanglingToolCalls(messages);

        Assert.Equal(before, messages);
    }
}

/// <summary>Packages the model adds need the user's OK; the user's own don't.</summary>
public class AgentPackageApprovalTests
{
    private const string Body = "public class S : Sketch { public override void Draw() { } }";

    [Fact]
    public void Packages_in_the_users_own_source_need_no_approval()
    {
        var context = new AgentToolContext();
        Assert.Empty(context.Unapproved("// nuget: Newtonsoft.Json, 13.0.3\n" + Body));
    }

    [Fact]
    public void A_package_the_agent_adds_needs_approval_once()
    {
        var context = new AgentToolContext();
        var user = "// nuget: Newtonsoft.Json, 13.0.3\n" + Body;
        var agent = "// nuget: Newtonsoft.Json, 13.0.3\n// nuget: Evil.Package\n" + Body;

        context.NoteSourceBeforeAgentEdit(user);
        context.NoteAgentWrote(agent);

        var pending = context.Unapproved(agent);
        Assert.Equal("Evil.Package", Assert.Single(pending).Id);

        context.Approve(pending);
        Assert.Empty(context.Unapproved(agent));
    }

    [Fact]
    public void Changing_the_version_counts_as_a_new_package()
    {
        var context = new AgentToolContext();
        context.NoteSourceBeforeAgentEdit("// nuget: Foo, 1.0.0\n" + Body);
        var agent = "// nuget: Foo, 2.0.0\n" + Body;
        context.NoteAgentWrote(agent);

        Assert.Equal("2.0.0", Assert.Single(context.Unapproved(agent)).Version);
    }

    [Fact]
    public void A_second_agent_edit_does_not_launder_its_own_unapproved_packages()
    {
        var context = new AgentToolContext();
        var first = "// nuget: Evil.Package\n" + Body;
        context.NoteSourceBeforeAgentEdit(Body);
        context.NoteAgentWrote(first);

        // The agent edits again without the user touching the editor in between.
        context.NoteSourceBeforeAgentEdit(first);
        var second = "// nuget: Evil.Package\n" + Body + "\n// otra cosa";
        context.NoteAgentWrote(second);

        Assert.Equal("Evil.Package", Assert.Single(context.Unapproved(second)).Id);
    }

    [Fact]
    public void Once_the_user_edits_the_agents_code_it_is_theirs()
    {
        var context = new AgentToolContext();
        var agent = "// nuget: Some.Package\n" + Body;
        context.NoteAgentWrote(agent);

        Assert.Empty(context.Unapproved(agent + "\n// tocado por el usuario"));
    }
}

/// <summary>NuGet: package contents can't escape the cache, and version requirements are honored.</summary>
public class NuGetSafetyTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "dana-nuget-test-root");

    [Theory]
    [InlineData("../outside.so")]
    [InlineData("a/../../outside.so")]
    [InlineData("../dana-nuget-test-root-sibling/x.so")]
    public void Paths_that_leave_the_root_are_refused(string relative)
    {
        Assert.Throws<InvalidDataException>(() => NuGetPackageResolver.CombineInside(Root, relative));
    }

    [Fact]
    public void Absolute_paths_are_refused()
    {
        var absolute = OperatingSystem.IsWindows() ? @"C:\Windows\evil.dll" : "/etc/evil.so";
        Assert.Throws<InvalidDataException>(() => NuGetPackageResolver.CombineInside(Root, absolute));
    }

    [Fact]
    public void Ordinary_nested_paths_stay_inside()
    {
        var path = NuGetPackageResolver.CombineInside(Root, Path.Combine("sub", "libfoo.so"));
        Assert.Equal(Path.Combine(Path.GetFullPath(Root), "sub", "libfoo.so"), path);
    }

    [Theory]
    [InlineData(null, "1.0.0", true)]
    [InlineData("1.2.0", "1.2.0", true)]
    [InlineData("1.2.0", "1.5.0", true)]   // a bare version means "this or newer"
    [InlineData("1.2.0", "1.1.0", false)]
    [InlineData("[2.0.0, 3.0.0)", "2.5.0", true)]
    [InlineData("[2.0.0, 3.0.0)", "3.0.0", false)]
    public void Version_requirements(string? spec, string version, bool satisfied)
    {
        Assert.Equal(satisfied, NuGetPackageResolver.SatisfiedBy(spec, NuGetVersion.Parse(version)));
    }
}

/// <summary>The saved API key isn't readable by other users of the machine.</summary>
public class AgentSettingsFileTests
{
    [Fact]
    public void Settings_file_is_owner_only_on_unix()
    {
        if (OperatingSystem.IsWindows())
            return; // per-user AppData ACLs cover it there

        var path = Path.Combine(Path.GetTempPath(), $"agent-settings-{Guid.NewGuid():N}.json");
        try
        {
            // An older version may have left it world-readable: saving must tighten it.
            File.WriteAllText(path, "{}");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            AgentSettingsStore.Save(new AgentSettings { ApiKey = "sk-test" }, path);

            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Contains("sk-test", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
