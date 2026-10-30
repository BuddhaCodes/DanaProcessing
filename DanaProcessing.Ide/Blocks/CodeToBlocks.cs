using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DanaProcessing.Ide.Localization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// What turning code back into blocks gave: the program, what didn't fit
    /// (line and text, so the child can see it), and values that weren't
    /// among a block's choices and were rounded to the nearest one.
    /// </summary>
    public sealed record CodeToBlocksResult(
        BlockProgram Program,
        IReadOnlyList<(int Line, string Code)> Skipped,
        IReadOnlyList<string> Rounded)
    {
        /// <summary>True when every line became a block, exactly.</summary>
        public bool Exact => Skipped.Count == 0 && Rounded.Count == 0;

        public int BlockCount => Program.AllBlocks().Count();
    }

    /// <summary>
    /// The way back: C# to blocks, for the kind of code the blocks make
    /// (what "Pasar a código" gives, and edits that stay within it). Each
    /// method a hat stands for becomes a column under that hat; each
    /// statement a block knows becomes that block; numbers that aren't one
    /// of a block's choices get the closest one. Anything else is reported,
    /// with its line, instead of being guessed at.
    /// </summary>
    public static class CodeToBlocks
    {
        public static CodeToBlocksResult Convert(string source, BlockView view = BlockView.Lines)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();
            var program = new BlockProgram { View = view };
            var reader = new Reader(program, tree);

            var type = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (type is null)
            {
                reader.Skip(root);
                return reader.Result();
            }
            program.Name = type.Identifier.Text;

            double y = 40;
            foreach (var member in type.Members)
            {
                switch (member)
                {
                    case FieldDeclarationSyntax:
                        break; // Turtle tortuga; int puntos; float lado... come back by themselves
                    case MethodDeclarationSyntax method when HatFor(method) is { } hat:
                    {
                        var stack = new BlockStack { X = 40, Y = y };
                        var hatBlock = program.NewBlock(hat.Type);
                        if (hat.Function is { } fn)
                            hatBlock.Values["fn"] = fn;
                        stack.Blocks.Add(hatBlock);
                        var statements = method.Body?.Statements ?? default;
                        reader.Statements(statements, stack.Blocks, Context(hat.Type));
                        // Setup and Draw are always there in a sketch: an empty one isn't a column.
                        if (stack.Blocks.Count > 1 || hat.Type is not (BlockCatalog.Start or BlockCatalog.Forever))
                        {
                            program.Stacks.Add(stack);
                            y += Height(stack.Blocks, view) + (view == BlockView.Lines ? 70 : 60);
                        }
                        break;
                    }
                    default:
                        reader.Skip(member);
                        break;
                }
            }
            return reader.Result();
        }

        private enum Where { Setup, Draw, Other, Music }

        private static Where Context(string hat) => hat switch
        {
            BlockCatalog.Start => Where.Setup,
            BlockCatalog.Forever => Where.Draw,
            BlockCatalog.MusicLoop => Where.Music,
            _ => Where.Other,
        };

        /// <summary>The hat a method stands for: Setup -> al empezar, Draw -> siempre... a [LiveLoop] -> música, Cuadrado() -> "para hacer un cuadrado".</summary>
        private static (string Type, string? Function)? HatFor(MethodDeclarationSyntax m)
        {
            string name = m.Identifier.Text;
            if (m.ParameterList.Parameters.Count > 0)
                return null;
            if (m.AttributeLists.SelectMany(a => a.Attributes).Any(a => a.Name.ToString() is "LiveLoop" or "LiveLoopAttribute"))
                return (BlockCatalog.MusicLoop, null);
            switch (name)
            {
                case "Setup": return (BlockCatalog.Start, null);
                case "Draw": return (BlockCatalog.Forever, null);
                case "MousePressed": return (BlockCatalog.Click, null);
                case "KeyPressed": return (BlockCatalog.Key, null);
            }
            if (SlotChoices.For(SlotKind.Function).FirstOrDefault(c => c.Code == name) is { } fn)
                return (BlockCatalog.Define, fn.Value);
            return null;
        }

        private static float Height(List<Block> blocks, BlockView view)
        {
            if (view == BlockView.Tiles)
                return 112 + blocks.Count(b => b.Def.Shape == BlockShape.CBlock) * 28;
            float rows = 0;
            void Count(IEnumerable<Block> list)
            {
                foreach (var b in list)
                {
                    rows++;
                    if (b.Def.Shape == BlockShape.CBlock)
                    {
                        rows++; // its "fin de" row
                        if (b.Body is { Count: > 0 } body) Count(body); else rows++;
                    }
                }
            }
            Count(blocks);
            return rows * 52;
        }

        private sealed class Reader
        {
            private readonly BlockProgram _program;
            private readonly SyntaxTree _tree;
            private readonly List<(int, string)> _skipped = new();
            private readonly List<string> _rounded = new();

            public Reader(BlockProgram program, SyntaxTree tree)
            {
                _program = program;
                _tree = tree;
            }

            public CodeToBlocksResult Result() => new(_program, _skipped, _rounded);

            public void Skip(SyntaxNode node)
            {
                int line = _tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
                var text = node.ToString().Split('\n')[0].Trim();
                if (text.Length > 70)
                    text = text[..70] + "…";
                _skipped.Add((line, text));
            }

            public void Statements(SyntaxList<StatementSyntax> statements, List<Block> into, Where where)
            {
                var list = statements.ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    int used = Statement(list, i, into, where);
                    if (used == 0)
                    {
                        Skip(list[i]);
                        used = 1;
                    }
                    i += used - 1;
                }
            }

            /// <summary>Reads the statement at <paramref name="i"/> (some blocks are several statements); returns how many it used, 0 = not a block.</summary>
            private int Statement(List<StatementSyntax> list, int i, List<Block> into, Where where)
            {
                var s = list[i];
                switch (s)
                {
                    case ForStatementSyntax f when RepeatCount(f) is { } times:
                    {
                        var b = Add(into, BlockCatalog.Repeat, ("times", times));
                        Statements(Body(f.Statement), b.Body!, where);
                        return 1;
                    }
                    case IfStatementSyntax ifs when ifs.Else is null:
                    {
                        var cond = Choice(SlotKind.Condition, ifs.Condition.ToString());
                        if (cond is null)
                            return 0;
                        var b = Add(into, BlockCatalog.If, ("cond", cond));
                        Statements(Body(ifs.Statement), b.Body!, where);
                        return 1;
                    }
                    case ExpressionStatementSyntax e:
                        return Expression(list, i, e.Expression, into, where);
                    case BlockSyntax block:
                        Statements(block.Statements, into, where);
                        return 1;
                    default:
                        return 0;
                }
            }

            private int Expression(List<StatementSyntax> list, int i, ExpressionSyntax expr, List<Block> into, Where where)
            {
                // await Sleep(x) -> esperar
                if (expr is AwaitExpressionSyntax { Expression: InvocationExpressionSyntax sleep } && Name(sleep) == "Sleep" && Args(sleep) is [var beats])
                {
                    // The one the blocks add on their own ("sin esperar, la música espera un tiempo sola") isn't a block.
                    if (list[i].GetTrailingTrivia().ToString().Contains("//", StringComparison.Ordinal) && beats == "1" && where == Where.Music)
                        return 1;
                    return Add(into, BlockCatalog.Wait, ("beats", NumberChoice(SlotKind.Beats, beats))) is null ? 0 : 1;
                }

                if (expr is AssignmentExpressionSyntax assign)
                    return Assignment(assign, into, where);

                if (expr is not InvocationExpressionSyntax call)
                    return 0;
                string name = Name(call);
                var args = Args(call);
                string? target = (call.Expression as MemberAccessExpressionSyntax)?.Expression.ToString();

                // Instrumentation and the setup the blocks always write: not blocks.
                if (name == "BlockHit" || (target == BlockCodeGenerator.TurtleField && name == "Draw"))
                    return 1;
                if (where == Where.Setup && target is null && (name is "Size" or "RectMode" || (name == "Background" && args is ["255"] && i <= 1)))
                    return 1;
                if (where == Where.Draw && target is null && name == "Background" && args is ["255"] && i == 0)
                    return 1;

                if (target == BlockCodeGenerator.TurtleField)
                {
                    return name switch
                    {
                        "Forward" when args.Length == 1 => One(into, BlockCatalog.Forward, ("steps", NumberChoice(SlotKind.Steps, args[0]))),
                        "Back" when args.Length == 1 => One(into, BlockCatalog.Back, ("steps", NumberChoice(SlotKind.Steps, args[0]))),
                        "TurnRight" when args.Length == 1 => One(into, BlockCatalog.Right, ("angle", NumberChoice(SlotKind.Angle, args[0]))),
                        "TurnLeft" when args.Length == 1 => One(into, BlockCatalog.Left, ("angle", NumberChoice(SlotKind.Angle, args[0]))),
                        "PenUp" => One(into, BlockCatalog.PenUp),
                        "PenDown" => One(into, BlockCatalog.PenDown),
                        "Home" => One(into, BlockCatalog.Home),
                        "PenSize" when args.Length == 1 => One(into, BlockCatalog.PenSize, ("size", NumberChoice(SlotKind.Thickness, args[0]))),
                        "PenColor" when ColorChoice(args) is { } c => One(into, BlockCatalog.PenColor, ("color", c)),
                        _ => 0,
                    };
                }
                if (target is not null)
                    return 0;

                switch (name)
                {
                    case "Background" when ColorChoice(args) is { } color:
                        return One(into, BlockCatalog.Background, ("color", color));
                    case "Fill" when args.Length == 3 && i + 1 < list.Count:
                    {
                        // Fill(color) + Circle/Square(place, size): one drawing block.
                        if (list[i + 1] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax shape }
                            && Name(shape) is "Circle" or "Square" && Args(shape) is { Length: 3 } sa
                            && ColorChoice(args) is { } fill && Choice(SlotKind.Place, sa[0] + ", " + sa[1]) is { } place)
                        {
                            Add(into, Name(shape) == "Circle" ? BlockCatalog.Circle : BlockCatalog.Square,
                                ("color", fill), ("size", NumberChoice(SlotKind.Size, sa[2])), ("place", place));
                            return 2;
                        }
                        // Fill(30, 30, 30); TextSize(..); Text("Puntos: " + puntos, ...) / Text("lado: " + lado, ...)
                        if (i + 2 < list.Count && list[i + 1] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax size } && Name(size) == "TextSize"
                            && list[i + 2] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax text } && Name(text) == "Text" && Args(text) is { Length: 3 } ta)
                        {
                            var shown = ta[0].Split('+').Last().Trim();
                            if (shown == BlockCodeGenerator.ScoreField)
                                return One(into, BlockCatalog.ScoreShow) * 3;
                            if (SlotChoices.Variables.FirstOrDefault(v => v.Code == shown) is { } variable)
                                return One(into, BlockCatalog.VarShow, ("var", variable.Value)) * 3;
                        }
                        return 0;
                    }
                    case "Play" when args.Length == 1 && Choice(SlotKind.Note, args[0]) is { } note:
                        return One(into, BlockCatalog.Note, ("note", note));
                    case "Sample" when args.Length == 1 && Choice(SlotKind.Drum, args[0]) is { } drum:
                        return One(into, BlockCatalog.Drum, ("drum", drum));
                    case "UseSynth" when args.Length == 1 && Choice(SlotKind.Instrument, args[0]) is { } synth:
                        return One(into, BlockCatalog.Instrument, ("instrument", synth));
                }
                // The child's own words: Cuadrado();
                if (args.Length == 0 && SlotChoices.For(SlotKind.Function).FirstOrDefault(c => c.Code == name) is { } fn)
                    return One(into, BlockCatalog.Call, ("fn", fn.Value));
                return 0;
            }

            private int Assignment(AssignmentExpressionSyntax a, List<Block> into, Where where)
            {
                string left = a.Left.ToString();
                string right = a.Right.ToString();
                if (left == BlockCodeGenerator.TurtleField + ".Block")
                    return 1; // instrumentation
                if (left == BlockCodeGenerator.TurtleField && right.Replace(" ", "") == "newTurtle(this)")
                    return 1;
                if (left == BlockCodeGenerator.ScoreField)
                {
                    if (right == "0")
                        return One(into, BlockCatalog.ScoreReset);
                    if (Squash(right) == Squash($"{left} + 1"))
                        return One(into, BlockCatalog.ScoreAdd);
                    return 0;
                }
                if (SlotChoices.Variables.FirstOrDefault(v => v.Code == left) is not { } variable)
                    return 0;
                // lado = lado + 10 / lado = lado - 5 / lado = 50 / lado = Random(1, 100)
                if (a.Right is BinaryExpressionSyntax bin && bin.Left.ToString() == left
                    && bin.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression)
                {
                    var amount = (bin.Kind() == SyntaxKind.SubtractExpression ? "-" : "") + bin.Right.ToString().Trim();
                    return One(into, BlockCatalog.VarChange, ("var", variable.Value), ("amount", NumberChoice(SlotKind.Amount, amount)));
                }
                return One(into, BlockCatalog.VarSet, ("var", variable.Value), ("value", NumberChoice(SlotKind.Number, right)));
            }

            private static SyntaxList<StatementSyntax> Body(StatementSyntax s) => s is BlockSyntax b ? b.Statements : SyntaxFactory.List(new[] { s });

            /// <summary>for (int i = 0; i &lt; N; i++) -> N as a "repetir" choice (any counter name).</summary>
            private string? RepeatCount(ForStatementSyntax f)
            {
                if (f.Declaration is not { Variables: [var v] } || v.Initializer?.Value.ToString() != "0")
                    return null;
                string counter = v.Identifier.Text;
                if (f.Condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LessThanExpression } cond || cond.Left.ToString() != counter)
                    return null;
                if (f.Incrementors is not [var inc])
                    return null;
                var step = Squash(inc.ToString());
                if (step != counter + "++" && step != "++" + counter)
                    return null;
                return NumberChoice(SlotKind.Count, cond.Right.ToString());
            }

            private Block Add(List<Block> into, string type, params (string Slot, string Value)[] values)
            {
                var b = _program.NewBlock(type);
                foreach (var (slot, value) in values)
                    b.Values[slot] = value;
                into.Add(b);
                return b;
            }

            private int One(List<Block> into, string type, params (string Slot, string Value)[] values)
            {
                Add(into, type, values);
                return 1;
            }

            private static string Name(InvocationExpressionSyntax call) => call.Expression switch
            {
                MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
                IdentifierNameSyntax id => id.Identifier.Text,
                _ => "",
            };

            private static string[] Args(InvocationExpressionSyntax call) => call.ArgumentList.Arguments.Select(a => a.ToString().Trim()).ToArray();

            private static string Squash(string code) => new(code.Where(c => !char.IsWhiteSpace(c)).ToArray());

            /// <summary>The choice whose code is <paramref name="code"/> (spaces don't matter), or null.</summary>
            private static string? Choice(SlotKind kind, string code) =>
                SlotChoices.For(kind).FirstOrDefault(c => Squash(c.Code) == Squash(code))?.Value;

            private string? ColorChoice(string[] args) => args.Length == 3 ? Choice(SlotKind.Color, string.Join(", ", args)) : null;

            /// <summary>
            /// A number slot's choice for this code: the exact one (a number,
            /// a variable, "al azar"), or for another number the closest
            /// choice, noting the change ("80 -> 75").
            /// </summary>
            private string NumberChoice(SlotKind kind, string code)
            {
                if (Choice(kind, code) is { } exact)
                    return exact;
                var choices = SlotChoices.For(kind);
                if (float.TryParse(code.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                {
                    var numeric = choices.Where(c => float.TryParse(c.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)).ToList();
                    if (numeric.Count > 0)
                    {
                        var best = numeric.OrderBy(c => Math.Abs(float.Parse(c.Value, CultureInfo.InvariantCulture) - n)).First();
                        _rounded.Add($"{code} → {best.Label}");
                        return best.Value;
                    }
                }
                var fallback = choices[0];
                _rounded.Add($"{code} → {fallback.Label}");
                return fallback.Value;
            }
        }
    }
}
