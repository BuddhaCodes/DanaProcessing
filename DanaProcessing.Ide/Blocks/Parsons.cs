using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DanaProcessing.Ide.Compilation;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// "Ordenar el código" -- the step between blocks and typing: the real
    /// C# lines of a program, shuffled, to put back in order. Nothing is
    /// typed, but now it's code. The check is the same as a turtle
    /// challenge's: the program the lines make has to draw the yellow path.
    /// Some puzzles add a line that doesn't belong.
    /// </summary>
    public sealed class ParsonsPuzzle
    {
        public required string Id { get; init; }
        public required string TitleEs { get; init; }
        public required string TitleEn { get; init; }
        public required string GoalEs { get; init; }
        public required string GoalEn { get; init; }

        /// <summary>The lines in a right order (the first, <c>void Setup() {</c>, stays in place).</summary>
        public required IReadOnlyList<string> Lines { get; init; }

        /// <summary>Lines that don't belong (they can stay in the pile).</summary>
        public IReadOnlyList<string> Extra { get; init; } = Array.Empty<string>();

        /// <summary>Fields the program needs, shown above (not to order): <c>float lado = 0;</c>.</summary>
        public IReadOnlyList<string> Fields { get; init; } = Array.Empty<string>();

        /// <summary>The turtle challenge whose drawing this program makes.</summary>
        public required string ShapeOf { get; init; }

        public string Title => Loc.Tr(TitleEs, TitleEn);
        public string Goal => Loc.Tr(GoalEs, GoalEn);

        public string ProgressId => "parsons:" + Id;

        public Challenge Shape => ChallengeCatalog.Get(ShapeOf) ?? throw new InvalidOperationException("no challenge " + ShapeOf);

        /// <summary>The lines to order, shuffled (never already in the right order), with the extra ones mixed in.</summary>
        public List<string> Shuffled(Random random)
        {
            var pile = Lines.Skip(1).Concat(Extra).ToList();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                for (int i = pile.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (pile[i], pile[j]) = (pile[j], pile[i]);
                }
                if (!pile.Take(Lines.Count - 1).SequenceEqual(Lines.Skip(1)))
                    break;
            }
            return pile;
        }
    }

    public sealed record ParsonsResult(bool Passed, string Message);

    public static class ParsonsChecker
    {
        public const string SetupLine = "void Setup() {";

        /// <summary>How deep each line goes: one step in after a line ending in "{", one out on a line starting with "}".</summary>
        public static IReadOnlyList<int> Indents(IReadOnlyList<string> lines)
        {
            var result = new List<int>();
            int depth = 0;
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.StartsWith('}'))
                    depth = Math.Max(0, depth - 1);
                result.Add(depth);
                if (t.EndsWith('{'))
                    depth++;
            }
            return result;
        }

        /// <summary>The whole sketch the ordered lines make (with the turtle, the fields, the faint guide).</summary>
        public static string Assemble(ParsonsPuzzle puzzle, IReadOnlyList<string> ordered, bool withGuide = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("public class OrdenarElCodigo : Sketch");
            sb.AppendLine("{");
            sb.AppendLine($"    Turtle {BlockCodeGenerator.TurtleField};");
            foreach (var f in puzzle.Fields)
                sb.AppendLine("    " + f);
            var indents = Indents(ordered);
            for (int i = 0; i < ordered.Count; i++)
            {
                var line = ordered[i].Trim();
                // What the child sees is the short "void Setup() {" (like the
                // Lines view); what runs also sizes the canvas and makes the turtle.
                if (line == SetupLine)
                    line = $"public override void Setup() {{ Size(600, 400); Background(255); {BlockCodeGenerator.TurtleField} = new Turtle(this);";
                sb.Append(' ', 4 + indents[i] * 4).AppendLine(line);
            }
            sb.AppendLine("    public override void Draw()");
            sb.AppendLine("    {");
            sb.AppendLine("        Background(255);");
            if (withGuide && puzzle.Shape.Scene() is { } scene)
                sb.AppendLine("        " + scene.DrawCode);
            sb.AppendLine($"        {BlockCodeGenerator.TurtleField}.Draw();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Checks an order: friendly messages for the usual mix-ups, then runs it and compares the drawing.</summary>
        public static ParsonsResult Check(ParsonsPuzzle puzzle, IReadOnlyList<string> ordered, out IReadOnlyList<TurtleLine> drawn)
        {
            drawn = Array.Empty<TurtleLine>();
            if (ordered.Count <= 1)
                return Fail(Loc.Tr("Pasá líneas de la pila a tu programa.", "Move lines from the pile into your program."));

            int open = ordered.Count(l => l.TrimEnd().EndsWith('{'));
            int close = ordered.Count(l => l.Trim().StartsWith('}'));
            if (open > close)
                return Fail(Loc.Tr("Falta cerrar una llave: cada { necesita su }.", "A brace isn't closed: every { needs its }."));
            if (close > open)
                return Fail(Loc.Tr("Sobra una llave }: cerraste algo que no estaba abierto.", "There's an extra }: you closed something that wasn't open."));
            var depth = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                var t = ordered[i].Trim();
                if (t.StartsWith('}'))
                    depth--;
                if (depth == 0 && i > 0 && !t.StartsWith('}') && !t.StartsWith("void ", StringComparison.Ordinal))
                    return Fail(Loc.Tr($"La línea {i + 1} quedó afuera de todo: tiene que ir adentro de un método.",
                                       $"Line {i + 1} is outside everything: it has to go inside a method."));
                if (t.EndsWith('{'))
                    depth++;
            }

            if (CallsItself(ordered) is { } method)
                return Fail(Loc.Tr($"{method} se llama a sí mismo: no terminaría nunca.", $"{method} calls itself: it would never end."));

            var compiled = SketchCompiler.Compile(Assemble(puzzle, ordered, withGuide: false));
            if (!compiled.Success)
                return Fail(Loc.Tr("Así no compila: fijate el orden de las llaves y de las líneas.", "That doesn't compile: check the order of the braces and lines."));
            try
            {
                drawn = HeadlessBlockRunner.RunTurtles(compiled.Sketch!, frames: 1);
            }
            finally
            {
                compiled.Sketch!.Dispose();
            }
            var lines = drawn.Where(l => l.Length > 0.5f).ToList();
            var target = puzzle.Shape.TargetLines();
            if (lines.Count == 0)
                return Fail(Loc.Tr("La tortuga no dibujó nada: ¿están las líneas adentro de Setup?", "The turtle didn't draw anything: are the lines inside Setup?"));
            if (!ChallengeChecker.Covers(lines, target))
                return Fail(Loc.Tr("¡Casi! A la tortuga le falta un pedazo del camino amarillo.", "Almost! The turtle missed part of the yellow path."));
            if (!ChallengeChecker.Covers(target, lines))
                return Fail(Loc.Tr("La tortuga dibujó por fuera del camino amarillo. Mirá el orden.", "The turtle drew outside the yellow path. Check the order."));
            return new ParsonsResult(true, Loc.Tr("¡Lo lograste! Ese es código C# de verdad.", "You did it! That's real C# code."));

            static ParsonsResult Fail(string message) => new(false, message);
        }

        /// <summary>The name of a method that calls itself (it would recurse forever), or null.</summary>
        internal static string? CallsItself(IReadOnlyList<string> ordered)
        {
            var open = new Stack<(string? Method, int Depth)>();
            int depth = 0;
            foreach (var raw in ordered)
            {
                var t = raw.Trim();
                if (t.StartsWith('}'))
                {
                    depth--;
                    while (open.Count > 0 && open.Peek().Depth > depth)
                        open.Pop();
                }
                foreach (var (method, _) in open)
                    if (method is not null && t.StartsWith(method + "(", StringComparison.Ordinal))
                        return method;
                if (t.EndsWith('{'))
                {
                    depth++;
                    string? name = null;
                    if (t.StartsWith("void ", StringComparison.Ordinal) && t.IndexOf('(') is > 5 and var paren)
                        name = t[5..paren].Trim();
                    open.Push((name, depth));
                }
            }
            return null;
        }
    }

    public static class ParsonsCatalog
    {
        private const string S = ParsonsChecker.SetupLine;
        private const string F100 = "tortuga.Forward(100);";
        private const string R90 = "tortuga.TurnRight(90);";
        private const string End = "}";

        public static readonly IReadOnlyList<ParsonsPuzzle> All = new[]
        {
            new ParsonsPuzzle
            {
                Id = "corner", ShapeOf = "corner",
                TitleEs = "La esquina en código", TitleEn = "The corner in code",
                GoalEs = "Ordená las líneas para que la tortuga llegue al final del camino.",
                GoalEn = "Put the lines in order so the turtle reaches the end of the path.",
                Lines = new[] { S, F100, R90, F100, End },
                Extra = new[] { "tortuga.TurnLeft(90);" },
            },
            new ParsonsPuzzle
            {
                Id = "square", ShapeOf = "repeat",
                TitleEs = "Repetir con for", TitleEn = "Repeat with for",
                GoalEs = "El bloque \"repetir 4 veces\" en C# es un for. Armá el cuadrado.",
                GoalEn = "The \"repeat 4 times\" block is a for in C#. Build the square.",
                Lines = new[] { S, "for (int i = 0; i < 4; i++) {", F100, R90, End, End },
            },
            new ParsonsPuzzle
            {
                Id = "jump", ShapeOf = "jump",
                TitleEs = "Saltar el charco", TitleEn = "Jump the puddle",
                GoalEs = "Levantá y bajá el lápiz en el momento justo.",
                GoalEn = "Lift and lower the pen at the right moment.",
                Lines = new[] { S, "tortuga.Forward(50);", "tortuga.PenUp();", "tortuga.Forward(50);", "tortuga.PenDown();", "tortuga.Forward(50);", End },
            },
            new ParsonsPuzzle
            {
                Id = "triangle", ShapeOf = "triangle",
                TitleEs = "El triángulo", TitleEn = "The triangle",
                GoalEs = "Tres lados iguales. Ojo: hay una línea que sobra.",
                GoalEn = "Three equal sides. Careful: one line doesn't belong.",
                Lines = new[] { S, "for (int i = 0; i < 3; i++) {", F100, "tortuga.TurnRight(120);", End, End },
                Extra = new[] { R90 },
            },
            new ParsonsPuzzle
            {
                Id = "stairs", ShapeOf = "stairs",
                TitleEs = "La escalera", TitleEn = "The staircase",
                GoalEs = "Tres escalones: cada uno sube, dobla, avanza y vuelve a doblar.",
                GoalEn = "Three steps: each one goes up, turns, goes on and turns back.",
                Lines = new[] { S, "for (int i = 0; i < 3; i++) {", "tortuga.Forward(50);", R90, "tortuga.Forward(50);", "tortuga.TurnLeft(90);", End, End },
            },
            new ParsonsPuzzle
            {
                Id = "star", ShapeOf = "star",
                TitleEs = "La estrella", TitleEn = "The star",
                GoalEs = "Cinco puntas, girando 144 en cada una. Hay un for que no sirve.",
                GoalEn = "Five points, turning 144 at each. One for loop is a trick.",
                Lines = new[] { S, "for (int i = 0; i < 5; i++) {", F100, "tortuga.TurnRight(144);", End, End },
                Extra = new[] { "for (int i = 0; i < 4; i++) {" },
            },
            new ParsonsPuzzle
            {
                Id = "windmill", ShapeOf = "windmill",
                TitleEs = "Un for adentro de otro", TitleEn = "A for inside a for",
                GoalEs = "El molinete: el for de adentro hace un cuadrado, el de afuera lo repite y gira.",
                GoalEn = "The pinwheel: the inner for makes a square, the outer one repeats it and turns.",
                Lines = new[] { S, "for (int j = 0; j < 4; j++) {", "for (int i = 0; i < 4; i++) {", "tortuga.Forward(50);", R90, End, R90, End, End },
            },
            new ParsonsPuzzle
            {
                Id = "spiral", ShapeOf = "spiral",
                TitleEs = "La espiral con una variable", TitleEn = "The spiral with a variable",
                GoalEs = "\"lado\" empieza en 10 y crece 10 en cada vuelta.",
                GoalEn = "\"lado\" starts at 10 and grows by 10 each time round.",
                Fields = new[] { "float lado = 0;" },
                Lines = new[] { S, "lado = 10;", "for (int i = 0; i < 12; i++) {", "tortuga.Forward(lado);", R90, "lado = lado + 10;", End, End },
            },
            new ParsonsPuzzle
            {
                Id = "method", ShapeOf = "two-squares",
                TitleEs = "Un método propio", TitleEn = "A method of your own",
                GoalEs = "Cuadrado() dibuja un cuadrado; Setup lo usa dos veces. Ordená los dos métodos.",
                GoalEn = "Cuadrado() draws a square; Setup uses it twice. Put both methods in order.",
                Lines = new[]
                {
                    S, "Cuadrado();", "tortuga.PenUp();", R90, "tortuga.Forward(75);", "tortuga.TurnLeft(90);", "tortuga.PenDown();", "Cuadrado();", End,
                    "void Cuadrado() {", "for (int i = 0; i < 4; i++) {", "tortuga.Forward(50);", R90, End, End,
                },
            },
        };

        public static ParsonsPuzzle? Get(string? id) => All.FirstOrDefault(p => p.Id == id);
    }
}
