using DanaProcessing.Reels;
using Xunit;

namespace DanaProcessing.Tests.Reels;

public class HighlighterTests
{
    private static string Show(string line)
    {
        var state = CSharpHighlighter.State.Normal;
        return string.Join(" ", CSharpHighlighter.Tokenize(line, ref state).Select(t => $"{line.Substring(t.Start, t.Length)}:{t.Kind}"));
    }

    [Fact]
    public void Basic_tokens()
    {
        var h = Show("var pull = new PVector(MouseX - p.Pos.X, 0.35f); // hi");
        Assert.Contains("var:Keyword", h);
        Assert.Contains("PVector:Method", h);
        Assert.Contains("MouseX:Plain", h);
        Assert.Contains("0.35f:Number", h);
        Assert.Contains("// hi:Comment", h);
    }

    [Fact]
    public void Types_generics_and_strings()
    {
        var h = Show("List<PVector> ps = new(); string s = $\"x={x}\";");
        Assert.Contains("List:Type", h);
        Assert.Contains("PVector:Type", h);
        Assert.Contains("string:Type", h);
        Assert.Contains("$\"x={x}\":String", h);
        Assert.Contains("Sketch:Type", Show("public class A : Sketch"));
    }

    [Fact]
    public void Block_comment_spans_lines()
    {
        var state = CSharpHighlighter.State.Normal;
        CSharpHighlighter.Tokenize("/* start", ref state);
        Assert.Equal(CSharpHighlighter.State.BlockComment, state);
        var next = CSharpHighlighter.Tokenize("still */ int x;", ref state);
        Assert.Equal(TokenKind.Comment, next[0].Kind);
        Assert.Contains(next, t => t.Kind == TokenKind.Type);
        Assert.Equal(CSharpHighlighter.State.Normal, state);
    }

    [Fact]
    public void Verbatim_string_spans_lines()
    {
        var state = CSharpHighlighter.State.Normal;
        CSharpHighlighter.Tokenize("var s = @\"line one", ref state);
        Assert.Equal(CSharpHighlighter.State.VerbatimString, state);
        var next = CSharpHighlighter.Tokenize("line \"\"two\"\" end\";", ref state);
        Assert.Equal(TokenKind.String, next[0].Kind);
        Assert.Equal(CSharpHighlighter.State.Normal, state);
    }

    [Fact]
    public void Tokens_never_overlap_or_leave_the_line()
    {
        foreach (var line in new[] { "if (a<b&&c>d) x+=0x1F;", "char c = '\\'';", "Fill(255, 176, 140); // @mark", "@\"unterminated", "" })
        {
            var state = CSharpHighlighter.State.Normal;
            int end = 0;
            foreach (var t in CSharpHighlighter.Tokenize(line, ref state))
            {
                Assert.True(t.Start >= end, line);
                Assert.True(t.Start + t.Length <= line.Length, line);
                end = t.Start + t.Length;
            }
        }
    }
}
