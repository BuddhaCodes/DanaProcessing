using DanaProcessing.Ide.Blocks;
using DanaProcessing.Ide.Compilation;
using Xunit;

namespace DanaProcessing.Tests.Ide;

public class ParsonsTests
{
    public static IEnumerable<object[]> Ids() => ParsonsCatalog.All.Select(p => new object[] { p.Id });

    [Theory]
    [MemberData(nameof(Ids))]
    public void The_right_order_draws_the_path_and_the_shuffle_does_not(string id)
    {
        var puzzle = ParsonsCatalog.Get(id)!;
        var right = ParsonsChecker.Check(puzzle, puzzle.Lines, out var drawn);
        Assert.True(right.Passed, right.Message + "\n" + ParsonsChecker.Assemble(puzzle, puzzle.Lines));
        Assert.NotEmpty(drawn);

        var pile = puzzle.Shuffled(new Random(7));
        Assert.Equal(puzzle.Lines.Count - 1 + puzzle.Extra.Count, pile.Count);
        Assert.NotEqual(puzzle.Lines.Skip(1), pile.Take(puzzle.Lines.Count - 1));

        // What the IDE runs (with the yellow guide) compiles too.
        Assert.True(SketchCompiler.Compile(ParsonsChecker.Assemble(puzzle, puzzle.Lines)).Success);
    }

    [Fact]
    public void Lines_are_indented_by_their_braces()
    {
        var lines = new[] { "void Setup() {", "for (int i = 0; i < 4; i++) {", "tortuga.Forward(100);", "}", "}" };
        Assert.Equal(new[] { 0, 1, 2, 1, 0 }, ParsonsChecker.Indents(lines));
    }

    [Fact]
    public void Usual_mix_ups_get_a_friendly_message()
    {
        var square = ParsonsCatalog.Get("square")!;
        string Msg(params string[] lines) => ParsonsChecker.Check(square, lines, out _).Message;

        Assert.Contains("llave", Msg("void Setup() {", "for (int i = 0; i < 4; i++) {", "tortuga.Forward(100);", "tortuga.TurnRight(90);", "}"));
        Assert.Contains("}", Msg("void Setup() {", "tortuga.Forward(100);", "}", "}"));
        Assert.Contains("afuera", Msg("void Setup() {", "}", "tortuga.Forward(100);", "for (int i = 0; i < 4; i++) {", "}"));
        // Right lines, wrong order: the turn first walks off the path.
        var wrong = ParsonsChecker.Check(square, new[] { "void Setup() {", "for (int i = 0; i < 4; i++) {", "tortuga.TurnRight(90);", "tortuga.Forward(100);", "}", "}" }, out var drawn);
        Assert.False(wrong.Passed);
        Assert.NotEmpty(drawn);
    }

    [Fact]
    public void A_method_that_calls_itself_is_stopped_before_it_runs()
    {
        var method = ParsonsCatalog.Get("method")!;
        var lines = new[]
        {
            "void Setup() {", "Cuadrado();", "}",
            "void Cuadrado() {", "for (int i = 0; i < 4; i++) {", "tortuga.Forward(50);", "Cuadrado();", "}", "}",
        };
        var result = ParsonsChecker.Check(method, lines, out _);
        Assert.False(result.Passed);
        Assert.Contains("Cuadrado", result.Message);
        Assert.Null(ParsonsChecker.CallsItself(method.Lines));
    }

    [Fact]
    public void The_extra_line_can_stay_in_the_pile()
    {
        var triangle = ParsonsCatalog.Get("triangle")!;
        Assert.NotEmpty(triangle.Extra);
        Assert.True(ParsonsChecker.Check(triangle, triangle.Lines, out _).Passed);
        var withExtra = triangle.Lines.Take(3).Append(triangle.Extra[0]).Concat(triangle.Lines.Skip(3)).ToList();
        Assert.False(ParsonsChecker.Check(triangle, withExtra, out _).Passed);
    }
}
