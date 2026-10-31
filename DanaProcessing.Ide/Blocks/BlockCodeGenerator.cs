using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>The C# a block program turns into, and which lines each block became.</summary>
    public sealed class GeneratedCode
    {
        public GeneratedCode(string source, IReadOnlyDictionary<int, (int First, int Last)> blockLines)
        {
            Source = source;
            BlockLines = blockLines;
        }

        public string Source { get; }

        /// <summary>Block id -> the 1-based lines its code spans (a C block or hat includes everything inside it).</summary>
        public IReadOnlyDictionary<int, (int First, int Last)> BlockLines { get; }

        /// <summary>The innermost block whose code covers <paramref name="line"/>, or null.</summary>
        public int? BlockAtLine(int line) =>
            BlockLines.Where(kv => kv.Value.First <= line && line <= kv.Value.Last)
                      .OrderBy(kv => kv.Value.Last - kv.Value.First)
                      .Select(kv => (int?)kv.Key)
                      .FirstOrDefault();
    }

    /// <summary>
    /// Extra code a challenge adds to a run (a faint guide the turtle should
    /// trace over, say). It goes into the code that RUNS, never into the code
    /// the child sees: that one is only what their own blocks say.
    /// </summary>
    public sealed record ChallengeScene(string DrawCode);

    /// <summary>
    /// Turns blocks into a real DanaProcessing sketch -- ordinary, readable C#
    /// a child can later keep writing by hand ("Pasar a código"). Each script
    /// (a column under a hat) becomes a method:
    ///   al empezar           -> Setup()
    ///   siempre              -> Draw()
    ///   al hacer clic        -> MousePressed()
    ///   al tocar una tecla   -> KeyPressed()
    ///   música que se repite -> a [LiveLoop] method
    /// Columns without a hat do nothing, like in Scratch. Several columns with
    /// the same hat run one after another, top to bottom.
    ///
    /// With <c>instrument: true</c> every block's code is preceded by
    /// BlockHit(id) (turtle commands get tortuga.Block = id instead, so they
    /// light up when the turtle actually walks, not when they're queued): the
    /// editor uses that to light up the blocks that are running.
    /// </summary>
    public static class BlockCodeGenerator
    {
        public const string TurtleField = "tortuga";
        public const string ScoreField = "puntos";

        public static GeneratedCode Generate(BlockProgram program, bool instrument = false, ChallengeScene? scene = null)
        {
            var g = new Writer(program, instrument);
            return g.Run(scene);
        }

        /// <summary>A valid C# class name from whatever the program is called ("Mi dibujo 2" -> "MiDibujo2").</summary>
        public static string ClassName(string? name)
        {
            var sb = new StringBuilder();
            bool upper = true;
            foreach (var ch in (name ?? "").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                    continue; // "dibujó" -> "dibujo"
                if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
                {
                    sb.Append(upper ? char.ToUpperInvariant(ch) : ch);
                    upper = false;
                }
                else
                {
                    upper = true;
                }
            }
            if (sb.Length == 0 || char.IsDigit(sb[0]))
                sb.Insert(0, "Programa");
            var result = sb.ToString();
            return result == "Sketch" || result == "Turtle" ? result + "1" : result;
        }

        private sealed class Writer
        {
            private readonly BlockProgram _program;
            private readonly bool _instrument;
            private readonly StringBuilder _sb = new();
            private readonly Dictionary<int, (int, int)> _lines = new();
            private int _line = 1;
            private int _indent;

            private readonly List<BlockStack> _scripts;
            private readonly bool _usesTurtle, _usesScore, _usesSquare;
            private readonly List<SlotChoice> _variables;
            // The child's own blocks: name -> the names it uses, and which
            // calls would loop forever (a word explained with itself).
            private readonly Dictionary<string, HashSet<string>> _calls = new();

            public Writer(BlockProgram program, bool instrument)
            {
                _program = program;
                _instrument = instrument;
                _scripts = program.Scripts().ToList();
                var running = _scripts.SelectMany(s => s.Blocks).SelectMany(b => b.SelfAndDescendants()).ToList();
                _usesTurtle = running.Any(b => b.Def.Category == BlockCategory.Turtle);
                _usesSquare = running.Any(b => b.Type == BlockCatalog.Square);
                _usesScore = running.Any(b => b.Def.Category == BlockCategory.Score
                                              || (b.Type == BlockCatalog.If && b.Value("cond") == "score10"));

                var usedNames = new HashSet<string>();
                foreach (var b in running)
                {
                    foreach (var slot in b.Def.Slots)
                    {
                        var value = b.Value(slot.Name);
                        if (slot.Kind == SlotKind.Variable)
                            usedNames.Add(value);
                        else if (value.StartsWith(SlotChoices.VariablePrefix, StringComparison.Ordinal))
                            usedNames.Add(value[SlotChoices.VariablePrefix.Length..]);
                        else if (slot.Kind == SlotKind.Condition && value == "counter10")
                            usedNames.Add("contador");
                    }
                }
                _variables = SlotChoices.Variables.Where(v => usedNames.Contains(v.Value)).ToList();

                foreach (var define in _scripts.Where(s => s.Hat!.Type == BlockCatalog.Define))
                {
                    var name = define.Hat!.Value("fn");
                    if (!_calls.TryGetValue(name, out var calls))
                        _calls[name] = calls = new HashSet<string>();
                    foreach (var b in define.Blocks.Skip(1).SelectMany(x => x.SelfAndDescendants()).Where(x => x.Type == BlockCatalog.Call))
                        calls.Add(b.Value("fn"));
                }
            }

            /// <summary>Whether <paramref name="from"/> ends up using <paramref name="target"/> (directly or through other words).</summary>
            private bool Reaches(string from, string target)
            {
                var seen = new HashSet<string>();
                var todo = new Stack<string>();
                todo.Push(from);
                while (todo.Count > 0)
                {
                    var n = todo.Pop();
                    if (!seen.Add(n) || !_calls.TryGetValue(n, out var next))
                        continue;
                    foreach (var m in next)
                    {
                        if (m == target)
                            return true;
                        todo.Push(m);
                    }
                }
                return false;
            }

            private string? _defining;

            private List<BlockStack> ScriptsFor(string hat) => _scripts.Where(s => s.Hat!.Type == hat).ToList();

            public GeneratedCode Run(ChallengeScene? scene)
            {
                Line(Loc.Tr("// Hecho con bloques en DanaProcessing.", "// Made with blocks in DanaProcessing."));
                Line($"public class {ClassName(_program.Name)} : Sketch");
                Line("{");
                _indent++;

                bool fields = false;
                if (_usesTurtle) { Line($"Turtle {TurtleField};"); fields = true; }
                if (_usesScore) { Line($"int {ScoreField} = 0;"); fields = true; }
                foreach (var v in _variables) { Line($"float {v.Code} = 0;"); fields = true; }
                if (fields) Blank();

                // Setup: always there -- the size and a white page come first,
                // then the turtle, then whatever "al empezar" says.
                Line("public override void Setup()");
                Open();
                Line("Size(600, 400);");
                Line("Background(255);");
                if (_usesSquare) Line("RectMode(ShapeAlignMode.Center);");
                if (_usesTurtle) Line($"{TurtleField} = new Turtle(this);");
                foreach (var script in ScriptsFor(BlockCatalog.Start))
                    Script(script);
                Close();

                // Draw: Sketch requires it even when nothing is drawn every frame.
                Blank();
                Line("public override void Draw()");
                Open();
                var forever = ScriptsFor(BlockCatalog.Forever);
                bool paintsBackground = forever.SelectMany(s => s.Blocks).SelectMany(b => b.SelfAndDescendants())
                                               .Any(b => b.Type == BlockCatalog.Background);
                if (_usesTurtle && !paintsBackground)
                    Line("Background(255);"); // the turtle redraws its lines every frame
                foreach (var script in forever)
                    Script(script);
                if (_usesTurtle)
                {
                    if (scene is not null && _instrument)
                        foreach (var l in scene.DrawCode.Replace("\r\n", "\n").Split('\n'))
                            Line(l);
                    Line($"{TurtleField}.Draw();");
                }
                Close();

                Event(BlockCatalog.Click, "public override void MousePressed()");
                Event(BlockCatalog.Key, "public override void KeyPressed()");

                // The child's own words, each a method: "para hacer un cuadrado" -> void Cuadrado().
                foreach (var group in ScriptsFor(BlockCatalog.Define).GroupBy(s => s.Hat!.Value("fn")))
                {
                    var choice = SlotChoices.Get(SlotKind.Function, group.Key);
                    Blank();
                    Line($"void {choice.Code}()");
                    Open();
                    _defining = group.Key;
                    foreach (var script in group)
                        Script(script);
                    _defining = null;
                    Close();
                }

                int n = 1;
                foreach (var loop in ScriptsFor(BlockCatalog.MusicLoop))
                {
                    Blank();
                    int first = _line;
                    Line("[LiveLoop]");
                    Line($"async Task Musica{n++}()");
                    Open();
                    Hit(loop.Hat!);
                    Statements(loop.Blocks.Skip(1), inMusic: true);
                    if (!loop.Blocks.Skip(1).SelectMany(b => b.SelfAndDescendants()).Any(b => b.Type == BlockCatalog.Wait))
                    {
                        // A live loop must wait somewhere or it would spin; one beat is what kids expect.
                        Line(Loc.Tr("await Sleep(1); // sin \"esperar\", la música espera un tiempo sola",
                                    "await Sleep(1); // with no \"wait\", the music waits one beat by itself"));
                    }
                    Close();
                    _lines[loop.Hat!.Id] = (first, _line - 1);
                }

                _indent--;
                Line("}");
                return new GeneratedCode(_sb.ToString(), _lines);
            }

            private void Event(string hat, string signature)
            {
                var scripts = ScriptsFor(hat);
                if (scripts.Count == 0)
                    return;
                Blank();
                Line(signature);
                Open();
                foreach (var script in scripts)
                    Script(script);
                Close();
            }

            /// <summary>A script's blocks inside an already-open method; the hat maps to those lines.</summary>
            private void Script(BlockStack script)
            {
                int first = _line;
                Hit(script.Hat!);
                Statements(script.Blocks.Skip(1), inMusic: false);
                _lines[script.Hat!.Id] = (first, Math.Max(first, _line - 1));
            }

            private void Statements(IEnumerable<Block> blocks, bool inMusic)
            {
                foreach (var block in blocks)
                    Statement(block, inMusic);
            }

            private void Statement(Block b, bool inMusic)
            {
                int first = _line;
                string Code(string slot) => SlotChoices.Get(b.Def.Slot(slot).Kind, b.Value(slot)).Code;
                string Name(string slot) => SlotChoices.Get(b.Def.Slot(slot).Kind, b.Value(slot)).Label;
                string T = TurtleField;

                if (b.Def.Category == BlockCategory.Turtle && _instrument)
                    Line($"{T}.Block = {b.Id};");
                else if (b.Def.Shape != BlockShape.Hat)
                    Hit(b);

                switch (b.Type)
                {
                    case BlockCatalog.Forward: Line($"{T}.Forward({Code("steps")});"); break;
                    case BlockCatalog.Back: Line($"{T}.Back({Code("steps")});"); break;
                    case BlockCatalog.Right: Line($"{T}.TurnRight({Code("angle")});"); break;
                    case BlockCatalog.Left: Line($"{T}.TurnLeft({Code("angle")});"); break;
                    case BlockCatalog.PenUp: Line($"{T}.PenUp();"); break;
                    case BlockCatalog.PenDown: Line($"{T}.PenDown();"); break;
                    case BlockCatalog.PenColor: Line($"{T}.PenColor({Code("color")}); // {Name("color")}"); break;
                    case BlockCatalog.PenSize: Line($"{T}.PenSize({Code("size")});"); break;
                    case BlockCatalog.Home: Line($"{T}.Home();"); break;

                    case BlockCatalog.Background: Line($"Background({Code("color")}); // {Name("color")}"); break;
                    case BlockCatalog.Circle:
                        Line($"Fill({Code("color")}); // {Name("color")}");
                        Line($"Circle({Code("place")}, {Code("size")});");
                        break;
                    case BlockCatalog.Square:
                        Line($"Fill({Code("color")}); // {Name("color")}");
                        Line($"Square({Code("place")}, {Code("size")});");
                        break;

                    case BlockCatalog.Repeat:
                    {
                        // Nested loops need different counter names: i, j, k...
                        string v = LoopVariable();
                        Line($"for (int {v} = 0; {v} < {Code("times")}; {v}++)");
                        Open();
                        _loopDepth++;
                        Statements(b.Body ?? new List<Block>(), inMusic);
                        _loopDepth--;
                        Close();
                        break;
                    }
                    case BlockCatalog.If:
                        Line($"if ({Code("cond")})");
                        Open();
                        Statements(b.Body ?? new List<Block>(), inMusic);
                        Close();
                        break;

                    case BlockCatalog.Note: Line($"Play({Code("note")});"); break;
                    case BlockCatalog.Drum: Line($"Sample({Code("drum")});"); break;
                    case BlockCatalog.Instrument: Line($"UseSynth({Code("instrument")});"); break;
                    case BlockCatalog.Wait:
                        if (inMusic)
                            Line($"await Sleep({Code("beats")});");
                        else
                            Line(Loc.Tr("// \"esperar\" solo funciona dentro de \"música que se repite\"",
                                        "// \"wait\" only works inside \"music that repeats\""));
                        break;

                    case BlockCatalog.VarSet: Line($"{Code("var")} = {Code("value")};"); break;
                    case BlockCatalog.VarChange:
                    {
                        string v = Code("var"), amount = Code("amount");
                        Line(amount.StartsWith('-') ? $"{v} = {v} - {amount[1..]};" : $"{v} = {v} + {amount};");
                        break;
                    }
                    case BlockCatalog.VarShow:
                    {
                        // Each variable on its own line of the canvas, so two "mostrar" don't overlap.
                        int row = SlotChoices.Variables.ToList().FindIndex(v => v.Value == b.Value("var"));
                        Line("Fill(30, 30, 30);");
                        Line("TextSize(28);");
                        Line($"Text(\"{Name("var")}: \" + {Code("var")}, 20, {40 + Math.Max(0, row) * 36});");
                        break;
                    }

                    case BlockCatalog.Call:
                    {
                        var fn = b.Value("fn");
                        string method = Code("fn");
                        if (!_calls.ContainsKey(fn))
                            Line(Loc.Tr($"// \"{Name("fn")}\" todavía no tiene bloques: armalo con \"para hacer {Name("fn")}\"",
                                        $"// \"{Name("fn")}\" has no blocks yet: build it with \"to make {Name("fn")}\""));
                        else if (_defining is { } current && (fn == current || Reaches(fn, current)))
                            // Would call itself forever (and crash): leave it out, and say why.
                            Line(Loc.Tr($"// \"{Name("fn")}\" no puede usarse dentro de sí mismo", $"// \"{Name("fn")}\" can't be used inside itself"));
                        else
                            Line($"{method}();");
                        break;
                    }

                    case BlockCatalog.ScoreReset: Line($"{ScoreField} = 0;"); break;
                    case BlockCatalog.ScoreAdd: Line($"{ScoreField} = {ScoreField} + 1;"); break;
                    case BlockCatalog.ScoreShow:
                        Line("Fill(30, 30, 30);");
                        Line("TextSize(32);");
                        Line($"Text({Loc.Tr("\"Puntos: \"", "\"Score: \"")} + {ScoreField}, 20, 45);");
                        break;

                    default:
                        // A hat in the middle of a column can't happen through the
                        // editor; ignore it rather than generate broken code.
                        break;
                }

                _lines[b.Id] = (first, _line - 1);
            }

            private int _loopDepth;
            private string LoopVariable() => _loopDepth < 3 ? ((char)('i' + _loopDepth)).ToString() : "n" + _loopDepth;

            private void Hit(Block b)
            {
                if (_instrument)
                    Line($"BlockHit({b.Id});");
            }

            private void Open() { Line("{"); _indent++; }
            private void Close() { _indent--; Line("}"); }
            private void Blank() { _sb.Append('\n'); _line++; }

            private void Line(string text)
            {
                _sb.Append(' ', _indent * 4).Append(text).Append('\n');
                _line++;
            }
        }
    }
}
