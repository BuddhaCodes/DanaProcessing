using System;
using System.Collections.Generic;
using System.Linq;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    public enum BlockShape
    {
        /// <summary>Starts a script ("al empezar", "siempre"...): nothing snaps above it.</summary>
        Hat,
        /// <summary>One instruction: snaps above and below.</summary>
        Statement,
        /// <summary>Wraps other blocks (repetir, si): has a mouth.</summary>
        CBlock,
    }

    public enum BlockCategory { Events, Turtle, Draw, Control, Music, Score, Variables, MyBlocks }

    /// <summary>What kind of value a slot holds -- each kind has its own fixed set of choices (see <see cref="SlotChoices"/>).</summary>
    public enum SlotKind { Steps, Angle, Count, Thickness, Color, Size, Place, Note, Drum, Beats, Instrument, Condition, Variable, Number, Amount, Function }

    public sealed record SlotDef(string Name, SlotKind Kind, string Default);

    /// <summary>
    /// One kind of block. <see cref="Label"/> is a template where "{name}"
    /// marks where a slot goes, e.g. "avanzar {steps}".
    /// </summary>
    public sealed record BlockDef(
        string Type,
        BlockCategory Category,
        BlockShape Shape,
        string Icon,
        string LabelEs,
        string LabelEn,
        IReadOnlyList<SlotDef> Slots)
    {
        public string Label => Loc.Tr(LabelEs, LabelEn);

        public SlotDef Slot(string name) =>
            Slots.FirstOrDefault(s => s.Name == name) ?? throw new ArgumentException($"Block '{Type}' has no slot '{name}'.");

        /// <summary>
        /// The label cut into text pieces and slot names, in order:
        /// "girar {angle} a la derecha" -> ("girar ", null), (null, "angle"), (" a la derecha", null).
        /// </summary>
        public IEnumerable<(string? Text, string? SlotName)> Parts()
        {
            var label = Label;
            int i = 0;
            while (i < label.Length)
            {
                int open = label.IndexOf('{', i);
                if (open < 0)
                {
                    yield return (label[i..], null);
                    yield break;
                }
                if (open > i)
                    yield return (label[i..open], null);
                int close = label.IndexOf('}', open);
                yield return (null, label[(open + 1)..close]);
                i = close + 1;
            }
        }
    }

    /// <summary>Every block the editor knows, by category, in palette order.</summary>
    public static class BlockCatalog
    {
        // Hats
        public const string Start = "event.start";
        public const string Forever = "event.forever";
        public const string Click = "event.click";
        public const string Key = "event.key";
        public const string MusicLoop = "music.loop";

        // Turtle
        public const string Forward = "turtle.forward";
        public const string Back = "turtle.back";
        public const string Right = "turtle.right";
        public const string Left = "turtle.left";
        public const string PenUp = "turtle.penup";
        public const string PenDown = "turtle.pendown";
        public const string PenColor = "turtle.color";
        public const string PenSize = "turtle.size";
        public const string Home = "turtle.home";

        // Drawing
        public const string Background = "draw.background";
        public const string Circle = "draw.circle";
        public const string Square = "draw.square";

        // Control
        public const string Repeat = "control.repeat";
        public const string If = "control.if";

        // Music
        public const string Note = "music.note";
        public const string Drum = "music.drum";
        public const string Wait = "music.wait";
        public const string Instrument = "music.instrument";

        // Variables (Renglones): a few named boxes that hold a number
        public const string VarSet = "var.set";
        public const string VarChange = "var.change";
        public const string VarShow = "var.show";

        // My blocks (Renglones): teach the turtle a new word, then use it
        public const string Define = "fn.define";
        public const string Call = "fn.call";

        // Score
        public const string ScoreReset = "score.reset";
        public const string ScoreAdd = "score.add";
        public const string ScoreShow = "score.show";

        private static SlotDef S(string name, SlotKind kind, string def) => new(name, kind, def);

        public static readonly IReadOnlyList<BlockDef> All = new[]
        {
            new BlockDef(Start, BlockCategory.Events, BlockShape.Hat, "▶", "al empezar", "when it starts", Array.Empty<SlotDef>()),
            new BlockDef(Forever, BlockCategory.Events, BlockShape.Hat, "🔁", "siempre", "forever", Array.Empty<SlotDef>()),
            new BlockDef(Click, BlockCategory.Events, BlockShape.Hat, "👆", "al hacer clic", "when clicked", Array.Empty<SlotDef>()),
            new BlockDef(Key, BlockCategory.Events, BlockShape.Hat, "⌨", "al tocar una tecla", "when a key is pressed", Array.Empty<SlotDef>()),

            new BlockDef(Forward, BlockCategory.Turtle, BlockShape.Statement, "⬆", "avanzar {steps} pasos", "move {steps} steps", new[] { S("steps", SlotKind.Steps, "100") }),
            new BlockDef(Back, BlockCategory.Turtle, BlockShape.Statement, "⬇", "retroceder {steps} pasos", "move back {steps} steps", new[] { S("steps", SlotKind.Steps, "100") }),
            new BlockDef(Right, BlockCategory.Turtle, BlockShape.Statement, "↻", "girar a la derecha {angle}", "turn right {angle}", new[] { S("angle", SlotKind.Angle, "90") }),
            new BlockDef(Left, BlockCategory.Turtle, BlockShape.Statement, "↺", "girar a la izquierda {angle}", "turn left {angle}", new[] { S("angle", SlotKind.Angle, "90") }),
            new BlockDef(PenUp, BlockCategory.Turtle, BlockShape.Statement, "✋", "levantar el lápiz", "pen up", Array.Empty<SlotDef>()),
            new BlockDef(PenDown, BlockCategory.Turtle, BlockShape.Statement, "✏", "bajar el lápiz", "pen down", Array.Empty<SlotDef>()),
            new BlockDef(PenColor, BlockCategory.Turtle, BlockShape.Statement, "🖍", "color del lápiz {color}", "pen color {color}", new[] { S("color", SlotKind.Color, "blue") }),
            new BlockDef(PenSize, BlockCategory.Turtle, BlockShape.Statement, "〰", "grosor del lápiz {size}", "pen size {size}", new[] { S("size", SlotKind.Thickness, "3") }),
            new BlockDef(Home, BlockCategory.Turtle, BlockShape.Statement, "🏠", "volver a casa", "go home", Array.Empty<SlotDef>()),

            new BlockDef(Background, BlockCategory.Draw, BlockShape.Statement, "🎨", "pintar el fondo {color}", "paint the background {color}", new[] { S("color", SlotKind.Color, "white") }),
            new BlockDef(Circle, BlockCategory.Draw, BlockShape.Statement, "⚪", "círculo {color} {size} en {place}", "circle {color} {size} at {place}",
                new[] { S("color", SlotKind.Color, "red"), S("size", SlotKind.Size, "50"), S("place", SlotKind.Place, "center") }),
            new BlockDef(Square, BlockCategory.Draw, BlockShape.Statement, "⬜", "cuadrado {color} {size} en {place}", "square {color} {size} at {place}",
                new[] { S("color", SlotKind.Color, "blue"), S("size", SlotKind.Size, "50"), S("place", SlotKind.Place, "center") }),

            new BlockDef(Repeat, BlockCategory.Control, BlockShape.CBlock, "🔂", "repetir {times} veces", "repeat {times} times", new[] { S("times", SlotKind.Count, "4") }),
            new BlockDef(If, BlockCategory.Control, BlockShape.CBlock, "❓", "si {cond}", "if {cond}", new[] { S("cond", SlotKind.Condition, "mouse") }),

            new BlockDef(MusicLoop, BlockCategory.Music, BlockShape.Hat, "🎵", "música que se repite", "music that repeats", Array.Empty<SlotDef>()),
            new BlockDef(Note, BlockCategory.Music, BlockShape.Statement, "🎹", "tocar la nota {note}", "play note {note}", new[] { S("note", SlotKind.Note, "C4") }),
            new BlockDef(Drum, BlockCategory.Music, BlockShape.Statement, "🥁", "tocar {drum}", "play {drum}", new[] { S("drum", SlotKind.Drum, "kick") }),
            new BlockDef(Wait, BlockCategory.Music, BlockShape.Statement, "⏳", "esperar {beats}", "wait {beats}", new[] { S("beats", SlotKind.Beats, "1") }),
            new BlockDef(Instrument, BlockCategory.Music, BlockShape.Statement, "🎸", "instrumento {instrument}", "instrument {instrument}", new[] { S("instrument", SlotKind.Instrument, "Pluck") }),

            new BlockDef(ScoreReset, BlockCategory.Score, BlockShape.Statement, "0️⃣", "puntos a cero", "score to zero", Array.Empty<SlotDef>()),
            new BlockDef(ScoreAdd, BlockCategory.Score, BlockShape.Statement, "⭐", "sumar un punto", "add a point", Array.Empty<SlotDef>()),
            new BlockDef(ScoreShow, BlockCategory.Score, BlockShape.Statement, "🔢", "mostrar los puntos", "show the score", Array.Empty<SlotDef>()),

            new BlockDef(VarSet, BlockCategory.Variables, BlockShape.Statement, "📦", "guardar {value} en {var}", "store {value} in {var}",
                new[] { S("value", SlotKind.Number, "50"), S("var", SlotKind.Variable, "lado") }),
            new BlockDef(VarChange, BlockCategory.Variables, BlockShape.Statement, "➕", "cambiar {var} en {amount}", "change {var} by {amount}",
                new[] { S("var", SlotKind.Variable, "lado"), S("amount", SlotKind.Amount, "10") }),
            new BlockDef(VarShow, BlockCategory.Variables, BlockShape.Statement, "👁", "mostrar {var}", "show {var}", new[] { S("var", SlotKind.Variable, "lado") }),

            new BlockDef(Define, BlockCategory.MyBlocks, BlockShape.Hat, "✨", "para hacer {fn}", "to make {fn}", new[] { S("fn", SlotKind.Function, "square") }),
            new BlockDef(Call, BlockCategory.MyBlocks, BlockShape.Statement, "🪄", "hacer {fn}", "make {fn}", new[] { S("fn", SlotKind.Function, "square") }),
        };

        /// <summary>
        /// Categories for Renglones (9-12): Fichas keeps them off its palette
        /// (a program that already uses them still shows them).
        /// </summary>
        public static bool IsForOlderKids(BlockCategory c) => c is BlockCategory.Variables or BlockCategory.MyBlocks;

        private static readonly Dictionary<string, BlockDef> ByType = All.ToDictionary(d => d.Type);

        public static bool Exists(string? type) => type != null && ByType.ContainsKey(type);

        public static BlockDef Get(string type) =>
            ByType.TryGetValue(type, out var def) ? def : throw new ArgumentException($"Unknown block type '{type}'.");

        public static IEnumerable<BlockDef> InCategory(BlockCategory category) => All.Where(d => d.Category == category);

        /// <summary>Order of the category buttons on the palette.</summary>
        public static readonly IReadOnlyList<BlockCategory> Categories = new[]
        {
            BlockCategory.Events, BlockCategory.Turtle, BlockCategory.Draw,
            BlockCategory.Control, BlockCategory.Music, BlockCategory.Score,
            BlockCategory.Variables, BlockCategory.MyBlocks,
        };

        public static string CategoryName(BlockCategory c) => c switch
        {
            BlockCategory.Events => Loc.Tr("Empezar", "Start"),
            BlockCategory.Turtle => Loc.Tr("Tortuga", "Turtle"),
            BlockCategory.Draw => Loc.Tr("Dibujar", "Draw"),
            BlockCategory.Control => Loc.Tr("Repetir", "Repeat"),
            BlockCategory.Music => Loc.Tr("Música", "Music"),
            BlockCategory.Score => Loc.Tr("Puntos", "Score"),
            BlockCategory.Variables => Loc.Tr("Variables", "Variables"),
            BlockCategory.MyBlocks => Loc.Tr("Inventar", "Invent"),
            _ => c.ToString(),
        };

        public static string CategoryIcon(BlockCategory c) => c switch
        {
            BlockCategory.Events => "▶",
            BlockCategory.Turtle => "🐢",
            BlockCategory.Draw => "🎨",
            BlockCategory.Control => "🔂",
            BlockCategory.Music => "🎵",
            BlockCategory.Score => "⭐",
            BlockCategory.Variables => "📦",
            BlockCategory.MyBlocks => "✨",
            _ => "•",
        };

        /// <summary>Category colors (RGB), bright and distinct, readable with dark text.</summary>
        public static (byte R, byte G, byte B) CategoryColor(BlockCategory c) => c switch
        {
            BlockCategory.Events => (255, 196, 61),
            BlockCategory.Turtle => (92, 196, 126),
            BlockCategory.Draw => (255, 128, 150),
            BlockCategory.Control => (255, 160, 70),
            BlockCategory.Music => (176, 140, 255),
            BlockCategory.Score => (86, 200, 220),
            BlockCategory.Variables => (150, 190, 60),
            BlockCategory.MyBlocks => (100, 140, 240),
            _ => (200, 200, 200),
        };
    }

    /// <summary>One option of a slot: what's stored, what's shown, and the C# it becomes.</summary>
    public sealed record SlotChoice(string Value, string LabelEs, string LabelEn, string Code, (byte R, byte G, byte B)? Swatch = null)
    {
        public string Label => Loc.Tr(LabelEs, LabelEn);
    }

    /// <summary>
    /// The fixed set of choices for each slot kind. Children pick from these
    /// -- nothing is ever typed -- so a slot can never hold something that
    /// doesn't compile.
    /// </summary>
    public static class SlotChoices
    {
        private static SlotChoice Num(string v, string? label = null) => new(v, label ?? v, label ?? v, v);

        private static SlotChoice Col(string v, string es, string en, byte r, byte g, byte b) =>
            new(v, es, en, $"{r}, {g}, {b}", (r, g, b));

        /// <summary>The variables a program can use: a few fixed names, so nothing is typed.</summary>
        public static readonly IReadOnlyList<SlotChoice> Variables = new[]
        {
            new SlotChoice("lado", "lado", "side", "lado"),
            new SlotChoice("angulo", "ángulo", "angle", "angulo"),
            new SlotChoice("contador", "contador", "counter", "contador"),
        };

        /// <summary>A variable as a value in a number slot ("avanzar {lado} pasos"): stored as "var:lado".</summary>
        public const string VariablePrefix = "var:";

        private static IEnumerable<SlotChoice> VariableUses() =>
            Variables.Select(v => new SlotChoice(VariablePrefix + v.Value, v.LabelEs, v.LabelEn, v.Code));

        /// <summary>True for a choice that isn't a fixed number: a variable or "al azar".</summary>
        public static bool IsSpecial(string? value) => value != null && (value.StartsWith(VariablePrefix, StringComparison.Ordinal) || value == "random");

        private static readonly Dictionary<SlotKind, IReadOnlyList<SlotChoice>> Choices = new()
        {
            [SlotKind.Steps] = new[] { Num("10"), Num("25"), Num("50"), Num("75"), Num("100"), Num("150"), Num("200"),
                                       new SlotChoice("random", "al azar", "random", "Random(10, 150)") }.Concat(VariableUses()).ToArray(),
            [SlotKind.Angle] = new[] { Num("30", "30°"), Num("45", "45°"), Num("60", "60°"), Num("72", "72°"), Num("90", "90°"), Num("120", "120°"), Num("144", "144°"), Num("180", "180°"),
                                       new SlotChoice("random", "al azar", "random", "Random(0, 360)") }.Concat(VariableUses()).ToArray(),
            [SlotKind.Count] = new[] { Num("2"), Num("3"), Num("4"), Num("5"), Num("6"), Num("8"), Num("10"), Num("12"), Num("36"),
                                       new SlotChoice("random", "al azar", "random", "(int)Random(2, 11)") }.Concat(VariableUses()).ToArray(),
            [SlotKind.Variable] = Variables,
            [SlotKind.Number] = new[] { Num("0"), Num("1"), Num("5"), Num("10"), Num("20"), Num("50"), Num("100"),
                                        new SlotChoice("random", "al azar", "random", "Random(1, 100)") },
            [SlotKind.Amount] = new[]
            {
                new SlotChoice("1", "+1", "+1", "1"), new SlotChoice("5", "+5", "+5", "5"), new SlotChoice("10", "+10", "+10", "10"),
                new SlotChoice("-1", "−1", "−1", "-1"), new SlotChoice("-5", "−5", "−5", "-5"), new SlotChoice("-10", "−10", "−10", "-10"),
            },
            // Names for the child's own blocks; the code is the method's name.
            [SlotKind.Function] = new[]
            {
                new SlotChoice("square", "un cuadrado", "a square", "Cuadrado"),
                new SlotChoice("triangle", "un triángulo", "a triangle", "Triangulo"),
                new SlotChoice("step", "un escalón", "a step", "Escalon"),
                new SlotChoice("flower", "una flor", "a flower", "Flor"),
                new SlotChoice("jump", "un salto", "a jump", "Salto"),
                new SlotChoice("trick", "mi truco", "my trick", "MiTruco"),
            },
            [SlotKind.Thickness] = new[]
            {
                new SlotChoice("1", "finito", "thin", "1"), new SlotChoice("3", "normal", "normal", "3"),
                new SlotChoice("6", "gordo", "thick", "6"), new SlotChoice("12", "gordísimo", "very thick", "12"),
            },
            [SlotKind.Color] = new[]
            {
                Col("red", "rojo", "red", 230, 57, 70), Col("orange", "naranja", "orange", 247, 140, 40),
                Col("yellow", "amarillo", "yellow", 250, 210, 40), Col("green", "verde", "green", 60, 170, 90),
                Col("blue", "azul", "blue", 50, 120, 220), Col("purple", "violeta", "purple", 140, 80, 200),
                Col("pink", "rosa", "pink", 245, 130, 190), Col("brown", "marrón", "brown", 140, 90, 50),
                Col("black", "negro", "black", 30, 30, 30), Col("white", "blanco", "white", 255, 255, 255),
            },
            [SlotKind.Size] = new[]
            {
                new SlotChoice("20", "chico", "small", "20"), new SlotChoice("50", "mediano", "medium", "50"),
                new SlotChoice("100", "grande", "big", "100"), new SlotChoice("200", "gigante", "huge", "200"),
            },
            [SlotKind.Place] = new[]
            {
                new SlotChoice("center", "el centro", "the center", "Width / 2, Height / 2"),
                new SlotChoice("mouse", "el mouse", "the mouse", "MouseX, MouseY"),
                new SlotChoice("random", "cualquier lado", "anywhere", "Random(Width), Random(Height)"),
                new SlotChoice("top", "arriba", "the top", "Width / 2, Height / 4"),
                new SlotChoice("bottom", "abajo", "the bottom", "Width / 2, Height * 3 / 4"),
            },
            [SlotKind.Note] = new[]
            {
                new SlotChoice("C4", "do", "C", "\"C4\""), new SlotChoice("D4", "re", "D", "\"D4\""),
                new SlotChoice("E4", "mi", "E", "\"E4\""), new SlotChoice("F4", "fa", "F", "\"F4\""),
                new SlotChoice("G4", "sol", "G", "\"G4\""), new SlotChoice("A4", "la", "A", "\"A4\""),
                new SlotChoice("B4", "si", "B", "\"B4\""), new SlotChoice("C5", "do agudo", "high C", "\"C5\""),
            },
            [SlotKind.Drum] = new[]
            {
                new SlotChoice("kick", "el bombo", "the kick", "\"kick\""),
                new SlotChoice("snare", "la caja", "the snare", "\"snare\""),
                new SlotChoice("hat", "el platillo", "the hi-hat", "\"hat\""),
            },
            [SlotKind.Beats] = new[]
            {
                new SlotChoice("0.25", "un cuartito", "a quarter beat", "0.25"),
                new SlotChoice("0.5", "medio tiempo", "half a beat", "0.5"),
                new SlotChoice("1", "un tiempo", "one beat", "1"),
                new SlotChoice("2", "dos tiempos", "two beats", "2"),
            },
            [SlotKind.Instrument] = new[]
            {
                new SlotChoice("Pluck", "guitarra", "guitar", "Synth.Pluck"),
                new SlotChoice("Triangle", "flauta", "flute", "Synth.Triangle"),
                new SlotChoice("Square", "videojuego", "video game", "Synth.Square"),
                new SlotChoice("Saw", "robot", "robot", "Synth.Saw"),
                new SlotChoice("Sine", "silbido", "whistle", "Synth.Sine"),
            },
            [SlotKind.Condition] = new[]
            {
                new SlotChoice("mouse", "el mouse está apretado", "the mouse is pressed", "IsMousePressed"),
                new SlotChoice("key", "hay una tecla apretada", "a key is pressed", "IsKeyPressed"),
                new SlotChoice("coin", "sale cara (al azar)", "heads (by chance)", "Random(1) < 0.5f"),
                new SlotChoice("score10", "hay 10 puntos o más", "the score is 10 or more", "puntos >= 10"),
                new SlotChoice("right", "la flecha → está apretada", "the → arrow is pressed", "IsKeyPressed && Key == CODED && KeyCode == RIGHT"),
                new SlotChoice("left", "la flecha ← está apretada", "the ← arrow is pressed", "IsKeyPressed && Key == CODED && KeyCode == LEFT"),
                new SlotChoice("up", "la flecha ↑ está apretada", "the ↑ arrow is pressed", "IsKeyPressed && Key == CODED && KeyCode == UP"),
                new SlotChoice("down", "la flecha ↓ está apretada", "the ↓ arrow is pressed", "IsKeyPressed && Key == CODED && KeyCode == DOWN"),
                new SlotChoice("space", "la barra espaciadora está apretada", "the space bar is pressed", "IsKeyPressed && Key == ' '"),
                new SlotChoice("mouseRight", "el mouse está a la derecha", "the mouse is on the right", "MouseX > Width / 2"),
                new SlotChoice("mouseTop", "el mouse está arriba", "the mouse is at the top", "MouseY < Height / 2"),
                new SlotChoice("counter10", "el contador llega a 10", "the counter reaches 10", "contador >= 10"),
            },
        };

        public static IReadOnlyList<SlotChoice> For(SlotKind kind) => Choices[kind];

        /// <summary>The choices a picker offers: Fichas leaves out the variables (they belong to Renglones).</summary>
        public static IReadOnlyList<SlotChoice> For(SlotKind kind, bool withVariables) =>
            withVariables ? Choices[kind] : Choices[kind].Where(c => !c.Value.StartsWith(VariablePrefix, StringComparison.Ordinal) && c.Value != "counter10").ToList();

        public static bool IsValid(SlotKind kind, string? value) => value != null && Choices[kind].Any(c => c.Value == value);

        public static SlotChoice Get(SlotKind kind, string value) =>
            Choices[kind].FirstOrDefault(c => c.Value == value) ?? Choices[kind][0];
    }
}
