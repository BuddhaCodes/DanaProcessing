using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>How a try at a challenge went: passed or not, 0-3 stars, and what to tell the child.</summary>
    public sealed record ChallengeResult(bool Passed, int Stars, string Message);

    /// <summary>
    /// A guided exercise: a short goal, only the blocks it needs on the
    /// palette, a starting program, and an automatic check. Turtle challenges
    /// show the shape to draw as a faint guide and compare the turtle's lines
    /// with it; the others look at how the program is built.
    /// </summary>
    public sealed class Challenge
    {
        public required string Id { get; init; }
        public required string TitleEs { get; init; }
        public required string TitleEn { get; init; }
        public required string GoalEs { get; init; }
        public required string GoalEn { get; init; }
        public required string HintEs { get; init; }
        public required string HintEn { get; init; }
        public required string Icon { get; init; }

        /// <summary>The only blocks on the palette (hats included).</summary>
        public required IReadOnlyList<string> Palette { get; init; }

        /// <summary>
        /// The turtle's path in a correct answer, drawn from the middle of the
        /// 600 x 400 canvas facing up. Null for challenges that aren't about a shape.
        /// </summary>
        public Action<Turtle>? Shape { get; init; }

        /// <summary>Blocks that must be used (e.g. "repetir" in the challenge about repeating).</summary>
        public IReadOnlyList<string> Required { get; init; } = Array.Empty<string>();

        /// <summary>For challenges without a shape: null if the program does the job, or what's still missing.</summary>
        public Func<BlockProgram, string?>? Rule { get; init; }

        /// <summary>The fewest blocks (not counting hats) a good answer needs: that's three stars.</summary>
        public int BestBlockCount { get; init; }

        /// <summary>The program the challenge starts from (just the hat, usually).</summary>
        public Func<BlockProgram>? Start { get; init; }

        public string Title => Loc.Tr(TitleEs, TitleEn);
        public string Goal => Loc.Tr(GoalEs, GoalEn);
        public string Hint => Loc.Tr(HintEs, HintEn);

        public bool IsTurtle => Shape is not null;

        public BlockProgram NewProgram()
        {
            var p = Start?.Invoke() ?? StartWith(BlockCatalog.Start);
            p.Name = Title;
            p.ChallengeId = Id;
            return p;
        }

        internal static BlockProgram StartWith(string hat)
        {
            var p = new BlockProgram();
            p.AddStack(40, 40, hat);
            return p;
        }

        /// <summary>The lines a correct answer draws (empty if this isn't a turtle challenge).</summary>
        public IReadOnlyList<TurtleLine> TargetLines()
        {
            if (Shape is null)
                return Array.Empty<TurtleLine>();
            var t = new Turtle(300, 200);
            Shape(t);
            t.Finish();
            return t.Lines;
        }

        /// <summary>The faint guide drawn under the turtle while trying this challenge.</summary>
        public ChallengeScene? Scene()
        {
            var lines = TargetLines();
            if (lines.Count == 0)
                return null;
            var sb = new StringBuilder();
            sb.Append("PushStyle(); StrokeCap(StrokeCapKind.Round); StrokeWeight(18); Stroke(255, 196, 61, 110);");
            foreach (var l in lines)
                sb.Append(CultureInfo.InvariantCulture, $" Line({l.X1:0.##}f, {l.Y1:0.##}f, {l.X2:0.##}f, {l.Y2:0.##}f);");
            sb.Append(" PopStyle();");
            return new ChallengeScene(sb.ToString());
        }
    }

    /// <summary>Checks an answer. Pure functions: tests call these directly.</summary>
    public static class ChallengeChecker
    {
        /// <summary>How far (pixels) a line may be from the guide and still count as on it.</summary>
        private const float Tolerance = 7f;

        /// <summary>
        /// Checks <paramref name="program"/> against <paramref name="challenge"/>.
        /// For a turtle challenge, <paramref name="turtleLines"/> is what the
        /// turtle drew (live, or from <see cref="HeadlessBlockRunner"/>).
        /// </summary>
        public static ChallengeResult Check(Challenge challenge, BlockProgram program, IReadOnlyList<TurtleLine>? turtleLines)
        {
            var used = program.Scripts().SelectMany(s => s.Blocks).SelectMany(b => b.SelfAndDescendants()).ToList();

            if (challenge.IsTurtle)
            {
                var drawn = (turtleLines ?? Array.Empty<TurtleLine>()).Where(l => l.Length > 0.5f).ToList();
                var target = challenge.TargetLines();
                if (drawn.Count == 0)
                    return Fail(Loc.Tr("La tortuga no dibujó nada todavía. ¿Pusiste los bloques debajo de \"al empezar\"?",
                                       "The turtle hasn't drawn anything yet. Did you put the blocks under \"when it starts\"?"));
                if (!Covers(drawn, target))
                    return Fail(Loc.Tr("¡Casi! A la tortuga le falta un pedazo del camino amarillo.",
                                       "Almost! The turtle missed part of the yellow path."));
                if (!Covers(target, drawn))
                    return Fail(Loc.Tr("La tortuga dibujó por fuera del camino amarillo. Mirá por dónde se fue.",
                                       "The turtle drew outside the yellow path. Look where it went."));
            }

            if (challenge.Rule?.Invoke(program) is { } missing)
                return Fail(missing);

            foreach (var type in challenge.Required)
            {
                if (!used.Any(b => b.Type == type))
                {
                    var def = BlockCatalog.Get(type);
                    return Fail(Loc.Tr($"¡Lo dibujaste! Ahora probá hacerlo usando el bloque \"{Strip(def.LabelEs)}\".",
                                       $"You drew it! Now try doing it with the \"{Strip(def.LabelEn)}\" block."));
                }
            }

            int count = used.Count(b => b.Def.Shape != BlockShape.Hat);
            int best = Math.Max(1, challenge.BestBlockCount);
            int stars = count <= best ? 3 : count <= best * 2 ? 2 : 1;
            string message = stars switch
            {
                3 => Loc.Tr("¡Lo lograste! ⭐⭐⭐", "You did it! ⭐⭐⭐"),
                2 => Loc.Tr($"¡Lo lograste! ⭐⭐ Se puede hacer con {best} bloques: ¿te animás?",
                            $"You did it! ⭐⭐ It can be done with {best} blocks: want to try?"),
                _ => Loc.Tr($"¡Lo lograste! ⭐ Se puede hacer con muchos menos bloques ({best}). ¿Probás con \"repetir\"?",
                            $"You did it! ⭐ It can be done with far fewer blocks ({best}). Try \"repeat\"?"),
            };
            return new ChallengeResult(true, stars, message);

            static ChallengeResult Fail(string message) => new(false, 0, message);
        }

        private static string Strip(string label)
        {
            int brace = label.IndexOf('{');
            return (brace < 0 ? label : label[..brace]).Trim();
        }

        /// <summary>True when every point along <paramref name="what"/> lies within the tolerance of some line in <paramref name="by"/>.</summary>
        internal static bool Covers(IReadOnlyList<TurtleLine> by, IReadOnlyList<TurtleLine> what)
        {
            foreach (var line in what)
            {
                int samples = Math.Max(2, (int)(line.Length / 4f) + 1);
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)(samples - 1);
                    float x = line.X1 + (line.X2 - line.X1) * t;
                    float y = line.Y1 + (line.Y2 - line.Y1) * t;
                    if (!by.Any(b => Distance(b, x, y) <= Tolerance))
                        return false;
                }
            }
            return true;
        }

        private static float Distance(TurtleLine l, float x, float y)
        {
            float dx = l.X2 - l.X1, dy = l.Y2 - l.Y1;
            float len2 = dx * dx + dy * dy;
            float t = len2 < 1e-6f ? 0 : Math.Clamp(((x - l.X1) * dx + (y - l.Y1) * dy) / len2, 0, 1);
            float px = l.X1 + dx * t - x, py = l.Y1 + dy * t - y;
            return MathF.Sqrt(px * px + py * py);
        }
    }

    /// <summary>The challenges, in the order a child meets them.</summary>
    public static class ChallengeCatalog
    {
        private static readonly string[] Walk = { BlockCatalog.Start, BlockCatalog.Forward };
        private static readonly string[] WalkTurn = { BlockCatalog.Start, BlockCatalog.Forward, BlockCatalog.Right, BlockCatalog.Left };
        private static readonly string[] WalkTurnRepeat = { BlockCatalog.Start, BlockCatalog.Forward, BlockCatalog.Right, BlockCatalog.Left, BlockCatalog.Repeat };

        private static IEnumerable<Block> In(BlockProgram p, string hat) =>
            p.Scripts().Where(s => s.Hat!.Type == hat).SelectMany(s => s.Blocks.Skip(1)).SelectMany(b => b.SelfAndDescendants());

        public static readonly IReadOnlyList<Challenge> All = new[]
        {
            new Challenge
            {
                Id = "first-step", Icon = "🐢",
                TitleEs = "El primer paso", TitleEn = "The first step",
                GoalEs = "Hacé que la tortuga camine por el camino amarillo.",
                GoalEn = "Make the turtle walk along the yellow path.",
                HintEs = "Arrastrá \"avanzar\" y pegalo debajo de \"al empezar\". Después apretá ▶.",
                HintEn = "Drag \"move\" and snap it under \"when it starts\". Then press ▶.",
                Palette = Walk, BestBlockCount = 1,
                Shape = t => t.Forward(100),
            },
            new Challenge
            {
                Id = "corner", Icon = "↱",
                TitleEs = "La esquina", TitleEn = "The corner",
                GoalEs = "Llevá a la tortuga hasta el final del camino. ¡Tiene que doblar!",
                GoalEn = "Take the turtle to the end of the path. It has to turn!",
                HintEs = "Avanzar, girar a la derecha, y avanzar otra vez.",
                HintEn = "Move, turn right, then move again.",
                Palette = WalkTurn, BestBlockCount = 3,
                Shape = t => { t.Forward(100); t.TurnRight(90); t.Forward(100); },
            },
            new Challenge
            {
                Id = "square", Icon = "⬜",
                TitleEs = "Un cuadrado", TitleEn = "A square",
                GoalEs = "Dibujá un cuadrado: cuatro lados iguales y cuatro esquinas.",
                GoalEn = "Draw a square: four equal sides and four corners.",
                HintEs = "Es como \"la esquina\", pero cuatro veces.",
                HintEn = "It's like \"the corner\", four times.",
                Palette = WalkTurn, BestBlockCount = 7,
                Shape = t => { for (int i = 0; i < 4; i++) { t.Forward(100); t.TurnRight(90); } },
            },
            new Challenge
            {
                Id = "repeat", Icon = "🔂",
                TitleEs = "¡A repetir!", TitleEn = "Repeat it!",
                GoalEs = "Dibujá el mismo cuadrado, pero con el bloque \"repetir\".",
                GoalEn = "Draw the same square, but with the \"repeat\" block.",
                HintEs = "Poné \"avanzar\" y \"girar\" ADENTRO de \"repetir 4\".",
                HintEn = "Put \"move\" and \"turn\" INSIDE \"repeat 4\".",
                Palette = WalkTurnRepeat, BestBlockCount = 3,
                Required = new[] { BlockCatalog.Repeat },
                Shape = t => { for (int i = 0; i < 4; i++) { t.Forward(100); t.TurnRight(90); } },
            },
            new Challenge
            {
                Id = "stairs", Icon = "🪜",
                TitleEs = "La escalera", TitleEn = "The staircase",
                GoalEs = "Subí la escalera de tres escalones.",
                GoalEn = "Climb the staircase with three steps.",
                HintEs = "Un escalón es: avanzar, girar a la derecha, avanzar, girar a la izquierda.",
                HintEn = "One step is: move, turn right, move, turn left.",
                Palette = WalkTurnRepeat, BestBlockCount = 5,
                Shape = t => { for (int i = 0; i < 3; i++) { t.Forward(50); t.TurnRight(90); t.Forward(50); t.TurnLeft(90); } },
            },
            new Challenge
            {
                Id = "triangle", Icon = "🔺",
                TitleEs = "El triángulo", TitleEn = "The triangle",
                GoalEs = "Dibujá un triángulo de tres lados iguales.",
                GoalEn = "Draw a triangle with three equal sides.",
                HintEs = "Tres lados, y en cada esquina hay que girar 120.",
                HintEn = "Three sides, and at each corner turn 120.",
                Palette = WalkTurnRepeat, BestBlockCount = 3,
                Shape = t => { for (int i = 0; i < 3; i++) { t.Forward(100); t.TurnRight(120); } },
            },
            new Challenge
            {
                Id = "star", Icon = "⭐",
                TitleEs = "La estrella", TitleEn = "The star",
                GoalEs = "Dibujá una estrella de cinco puntas.",
                GoalEn = "Draw a five-pointed star.",
                HintEs = "Repetir 5 veces: avanzar y girar 144.",
                HintEn = "Repeat 5 times: move and turn 144.",
                Palette = WalkTurnRepeat, BestBlockCount = 3,
                Shape = t => { for (int i = 0; i < 5; i++) { t.Forward(100); t.TurnRight(144); } },
            },
            new Challenge
            {
                Id = "jump", Icon = "🦘",
                TitleEs = "Saltar el charco", TitleEn = "Jump the puddle",
                GoalEs = "Dibujá los dos pedazos de camino, sin pintar en el medio.",
                GoalEn = "Draw both pieces of path without painting the gap.",
                HintEs = "Levantá el lápiz para cruzar sin dibujar, y bajalo del otro lado.",
                HintEn = "Lift the pen to cross without drawing, then put it down on the other side.",
                Palette = new[] { BlockCatalog.Start, BlockCatalog.Forward, BlockCatalog.PenUp, BlockCatalog.PenDown },
                BestBlockCount = 5,
                Shape = t => { t.Forward(50); t.PenUp(); t.Forward(50); t.PenDown(); t.Forward(50); },
            },
            new Challenge
            {
                Id = "colors", Icon = "🖍",
                TitleEs = "Lápices de colores", TitleEn = "Colored pencils",
                GoalEs = "Dibujá el cuadrado con al menos dos colores distintos.",
                GoalEn = "Draw the square with at least two different colors.",
                HintEs = "Usá \"color del lápiz\" antes de dibujar un lado.",
                HintEn = "Use \"pen color\" before drawing a side.",
                Palette = new[] { BlockCatalog.Start, BlockCatalog.Forward, BlockCatalog.Right, BlockCatalog.Repeat, BlockCatalog.PenColor, BlockCatalog.PenSize },
                BestBlockCount = 7,
                Required = new[] { BlockCatalog.PenColor },
                Shape = t => { for (int i = 0; i < 4; i++) { t.Forward(100); t.TurnRight(90); } },
                Rule = p => In(p, BlockCatalog.Start).Where(b => b.Type == BlockCatalog.PenColor).Select(b => b.Value("color")).Distinct().Count() >= 2
                    ? null : Loc.Tr("Usá por lo menos dos colores distintos.", "Use at least two different colors."),
            },
            new Challenge
            {
                Id = "beat", Icon = "🥁",
                TitleEs = "Mi primer ritmo", TitleEn = "My first beat",
                GoalEs = "Hacé un ritmo que se repita: bombo, esperar, caja, esperar.",
                GoalEn = "Make a beat that repeats: kick, wait, snare, wait.",
                HintEs = "Usá \"tocar\" y \"esperar\" dentro de \"música que se repite\".",
                HintEn = "Use \"play\" and \"wait\" inside \"music that repeats\".",
                Palette = new[] { BlockCatalog.MusicLoop, BlockCatalog.Drum, BlockCatalog.Wait },
                BestBlockCount = 4,
                Start = () => Challenge.StartWith(BlockCatalog.MusicLoop),
                Rule = p =>
                {
                    var blocks = In(p, BlockCatalog.MusicLoop).ToList();
                    var drums = blocks.Where(b => b.Type == BlockCatalog.Drum).Select(b => b.Value("drum")).ToList();
                    if (drums.Count < 2)
                        return Loc.Tr("Un ritmo necesita por lo menos dos golpes de tambor.", "A beat needs at least two drum hits.");
                    if (drums.Distinct().Count() < 2)
                        return Loc.Tr("Probá con dos tambores distintos, como el bombo y la caja.", "Try two different drums, like the kick and the snare.");
                    if (blocks.Count(b => b.Type == BlockCatalog.Wait) < 2)
                        return Loc.Tr("Poné un \"esperar\" después de cada golpe, así no suenan todos juntos.", "Put a \"wait\" after each hit so they don't all sound at once.");
                    return null;
                },
            },
            new Challenge
            {
                Id = "song", Icon = "🎹",
                TitleEs = "Una melodía", TitleEn = "A melody",
                GoalEs = "Tocá do, re, mi... ¡por lo menos tres notas distintas!",
                GoalEn = "Play C, D, E... at least three different notes!",
                HintEs = "Nota, esperar, nota, esperar... y elegí el instrumento que más te guste.",
                HintEn = "Note, wait, note, wait... and pick the instrument you like best.",
                Palette = new[] { BlockCatalog.MusicLoop, BlockCatalog.Note, BlockCatalog.Wait, BlockCatalog.Instrument },
                BestBlockCount = 6,
                Start = () => Challenge.StartWith(BlockCatalog.MusicLoop),
                Rule = p =>
                {
                    var blocks = In(p, BlockCatalog.MusicLoop).ToList();
                    if (blocks.Where(b => b.Type == BlockCatalog.Note).Select(b => b.Value("note")).Distinct().Count() < 3)
                        return Loc.Tr("Necesitás tres notas distintas.", "You need three different notes.");
                    if (blocks.Count(b => b.Type == BlockCatalog.Wait) < 2)
                        return Loc.Tr("Poné \"esperar\" entre las notas, así se escuchan una por una.", "Put \"wait\" between the notes so you hear them one by one.");
                    return null;
                },
            },
            new Challenge
            {
                Id = "paint", Icon = "🖌",
                TitleEs = "Pintar con el mouse", TitleEn = "Paint with the mouse",
                GoalEs = "Hacé que, al mover el mouse, quede pintado un caminito de círculos.",
                GoalEn = "Make a trail of circles appear wherever you move the mouse.",
                HintEs = "\"siempre\" con un círculo en \"el mouse\".",
                HintEn = "\"forever\" with a circle at \"the mouse\".",
                Palette = new[] { BlockCatalog.Forever, BlockCatalog.Circle, BlockCatalog.Square, BlockCatalog.Background },
                BestBlockCount = 1,
                Start = () => Challenge.StartWith(BlockCatalog.Forever),
                Rule = p =>
                {
                    var blocks = In(p, BlockCatalog.Forever).ToList();
                    if (!blocks.Any(b => (b.Type == BlockCatalog.Circle || b.Type == BlockCatalog.Square) && b.Value("place") == "mouse"))
                        return Loc.Tr("Dibujá un círculo (o un cuadrado) en \"el mouse\".", "Draw a circle (or a square) at \"the mouse\".");
                    if (blocks.Any(b => b.Type == BlockCatalog.Background))
                        return Loc.Tr("\"pintar el fondo\" borra todo en cada cuadro: sacalo para que quede el caminito.",
                                      "\"paint the background\" wipes everything each frame: take it out to keep the trail.");
                    return null;
                },
            },
            new Challenge
            {
                Id = "points", Icon = "⭐",
                TitleEs = "Clic y puntos", TitleEn = "Click and score",
                GoalEs = "Cada clic suma un punto. ¡Y que se vean los puntos!",
                GoalEn = "Every click adds a point. And show the score!",
                HintEs = "\"al hacer clic\" suma un punto; \"siempre\" pinta el fondo y muestra los puntos.",
                HintEn = "\"when clicked\" adds a point; \"forever\" paints the background and shows the score.",
                Palette = new[] { BlockCatalog.Forever, BlockCatalog.Click, BlockCatalog.ScoreAdd, BlockCatalog.ScoreShow, BlockCatalog.Background, BlockCatalog.Circle },
                BestBlockCount = 3,
                Start = () =>
                {
                    var p = Challenge.StartWith(BlockCatalog.Click);
                    p.AddStack(40, 220, BlockCatalog.Forever);
                    return p;
                },
                Rule = p =>
                {
                    if (!In(p, BlockCatalog.Click).Any(b => b.Type == BlockCatalog.ScoreAdd))
                        return Loc.Tr("Al hacer clic tiene que sumar un punto.", "Clicking has to add a point.");
                    if (!In(p, BlockCatalog.Forever).Any(b => b.Type == BlockCatalog.ScoreShow))
                        return Loc.Tr("Mostrá los puntos dentro de \"siempre\", así se actualizan.", "Show the score inside \"forever\" so it updates.");
                    return null;
                },
            },
        };

        public static Challenge? Get(string? id) => All.FirstOrDefault(c => c.Id == id);

        /// <summary>The challenge after <paramref name="id"/>, or null at the end.</summary>
        public static Challenge? Next(string id)
        {
            int i = All.ToList().FindIndex(c => c.Id == id);
            return i >= 0 && i + 1 < All.Count ? All[i + 1] : null;
        }
    }

    /// <summary>Best stars per challenge, kept in the IDE's settings folder.</summary>
    public static class ChallengeProgress
    {
        internal static string FilePath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DanaProcessingIde", "blocks-progress.json");

        public static Dictionary<string, int> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(FilePath)) ?? new();
            }
            catch
            {
                // A damaged file shouldn't stop anyone from playing: start over.
            }
            return new();
        }

        /// <summary>Records a result, keeping the best one. Returns true if it's a new best.</summary>
        public static bool Record(string challengeId, int stars)
        {
            var all = Load();
            if (all.TryGetValue(challengeId, out var best) && best >= stars)
                return false;
            all[challengeId] = stars;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(all));
            }
            catch
            {
                // Progress is a nicety; failing to save it must not interrupt the lesson.
            }
            return true;
        }
    }
}
