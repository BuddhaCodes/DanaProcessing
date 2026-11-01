using System;
using System.Collections.Generic;
using System.Linq;
using DanaProcessing.Ide.Editor;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// Examples made of blocks, at the top of the Examples window: colorful
    /// things to open, run and change. They open as blocks (in the view that
    /// suits them); the preview runs their C#.
    /// </summary>
    public static class BlockSamples
    {
        public static IReadOnlyList<SketchSample> All => _all ??= Build();
        private static IReadOnlyList<SketchSample>? _all;

        private static IReadOnlyList<SketchSample> Build() => new[]
        {
            Sample(Loc.Tr("Flor arcoíris", "Rainbow flower"),
                Loc.Tr("Doce cuadrados que giran, cada uno de un color del arcoíris. Una palabra nueva, «un cuadrado», hace el trabajo.",
                       "Twelve squares turning, each a color of the rainbow. A new word, \"a square\", does the work."),
                RainbowFlower, SampleTag.Generative, SampleTag.Shapes),
            Sample(Loc.Tr("Espiral de colores", "Color spiral"),
                Loc.Tr("Cada lado un poco más largo que el anterior, y el color cambia cada diez. Una variable, «lado», va creciendo.",
                       "Each side a bit longer than the last, and the color changes every ten. A variable, \"side\", keeps growing."),
                ColorSpiral, SampleTag.Generative, SampleTag.Animation),
            Sample(Loc.Tr("Mandala", "Mandala"),
                Loc.Tr("Hexágonos que dan la vuelta, en turquesa y fucsia, con el lápiz grueso.", "Hexagons going round, in turquoise and magenta, with a thick pen."),
                Mandala, SampleTag.Generative, SampleTag.Shapes),
            Sample(Loc.Tr("Sol de verano", "Summer sun"),
                Loc.Tr("Un sol dorado: un círculo hecho de pasitos y rayos que salen del centro.", "A golden sun: a circle made of tiny steps and rays coming out of the middle."),
                SummerSun, SampleTag.Shapes),
            Sample(Loc.Tr("Constelación", "Constellation"),
                Loc.Tr("Ocho estrellas alrededor del centro, cada una de un color.", "Eight stars around the middle, each one a different color."),
                Constellation, SampleTag.Shapes, SampleTag.Generative),
            Sample(Loc.Tr("Lluvia de burbujas", "Bubble rain"),
                Loc.Tr("Burbujas de colores que aparecen solas en cualquier lado, sobre un cielo azul marino. Hacé clic para empezar de nuevo.",
                       "Colored bubbles popping up anywhere by themselves, on a navy sky. Click to start over."),
                BubbleRain, SampleTag.Animation, SampleTag.Interaction),
            Sample(Loc.Tr("Pincel mágico", "Magic brush"),
                Loc.Tr("Pintá moviendo el mouse: con el botón apretado pinta fucsia, si no, celeste. Con cualquier tecla, la hoja queda en blanco.",
                       "Paint by moving the mouse: magenta with the button down, sky blue otherwise. Any key gives you a clean page."),
                MagicBrush, SampleTag.Interaction),
            Sample(Loc.Tr("Juego: manejá la tortuga", "Game: steer the turtle"),
                Loc.Tr("Las flechas mueven la tortuga y dibujan un camino. ¡Hacé tu propio laberinto!", "The arrows move the turtle and draw a path. Make your own maze!"),
                SteerGame, SampleTag.Games, SampleTag.Interaction),
            Sample(Loc.Tr("Banda de rock", "Rock band"),
                Loc.Tr("Dos músicas a la vez: la batería en una, y una melodía de videojuego en la otra.", "Two tunes at once: the drums in one, and a video-game melody in the other."),
                RockBand, SampleTag.Audio),
        };

        private static SketchSample Sample(string name, string description, Func<BlockProgram> make, params SampleTag[] tags)
        {
            BlockProgram Named()
            {
                var p = make();
                p.Name = name;
                return p;
            }
            return new SketchSample(name, description, BlockCodeGenerator.Generate(Named()).Source, new[] { SampleTag.Blocks }.Concat(tags).ToArray())
            {
                Blocks = Named,
            };
        }

        // =================================================================
        // A tiny way to write block programs
        // =================================================================

        private sealed class Writer
        {
            public readonly BlockProgram P;
            public Writer(BlockView view) => P = new BlockProgram { View = view };

            public Block B(string type, params (string Slot, string Value)[] values)
            {
                var b = P.NewBlock(type);
                foreach (var (slot, value) in values)
                    b.Values[slot] = value;
                return b;
            }

            public Block Rep(string times, params Block[] body)
            {
                var b = B(BlockCatalog.Repeat, ("times", times));
                b.Body!.AddRange(body);
                return b;
            }

            public Block If(string cond, params Block[] body)
            {
                var b = B(BlockCatalog.If, ("cond", cond));
                b.Body!.AddRange(body);
                return b;
            }

            public Block F(string steps) => B(BlockCatalog.Forward, ("steps", steps));
            public Block Back(string steps) => B(BlockCatalog.Back, ("steps", steps));
            public Block R(string angle) => B(BlockCatalog.Right, ("angle", angle));
            public Block L(string angle) => B(BlockCatalog.Left, ("angle", angle));
            public Block Color(string color) => B(BlockCatalog.PenColor, ("color", color));
            public Block Size(string size) => B(BlockCatalog.PenSize, ("size", size));

            public void Script(double x, double y, string hat, params Block[] blocks) => Script(x, y, hat, null, blocks);

            public void Script(double x, double y, string hat, string? fn, params Block[] blocks)
            {
                var stack = P.AddStack(x, y, hat);
                if (fn is not null)
                    stack.Blocks[0].Values["fn"] = fn;
                stack.Blocks.AddRange(blocks);
            }
        }

        private static readonly string[] Rainbow = { "red", "orange", "yellow", "green", "blue", "purple" };

        private static BlockProgram RainbowFlower()
        {
            var w = new Writer(BlockView.Lines);
            var petals = new List<Block> { w.Size("3") };
            foreach (var c in Rainbow.Concat(Rainbow))
            {
                petals.Add(w.Color(c));
                petals.Add(w.B(BlockCatalog.Call, ("fn", "square")));
                petals.Add(w.R("30"));
            }
            w.Script(40, 40, BlockCatalog.Start, petals.ToArray());
            w.Script(40, 2200, BlockCatalog.Define, "square", w.Rep("4", w.F("100"), w.R("90")));
            return w.P;
        }

        private static BlockProgram ColorSpiral()
        {
            var w = new Writer(BlockView.Lines);
            var body = new List<Block> { w.B(BlockCatalog.VarSet, ("value", "5"), ("var", "lado")), w.Size("3") };
            foreach (var c in Rainbow)
            {
                body.Add(w.Color(c));
                body.Add(w.Rep("10", w.B(BlockCatalog.Forward, ("steps", "var:lado")), w.R("90"),
                    w.B(BlockCatalog.VarChange, ("var", "lado"), ("amount", "2"))));
            }
            w.Script(40, 40, BlockCatalog.Start, body.ToArray());
            return w.P;
        }

        private static BlockProgram Mandala()
        {
            var w = new Writer(BlockView.Tiles);
            w.Script(40, 40, BlockCatalog.Start,
                w.Size("6"),
                w.Rep("6",
                    w.Color("teal"), w.Rep("6", w.F("60"), w.R("60")), w.R("30"),
                    w.Color("magenta"), w.Rep("6", w.F("60"), w.R("60")), w.R("30")));
            return w.P;
        }

        private static BlockProgram SummerSun()
        {
            var w = new Writer(BlockView.Tiles);
            w.Script(40, 40, BlockCatalog.Start,
                w.Color("gold"), w.Size("9"),
                w.Rep("12", w.F("120"), w.Back("120"), w.R("30")),
                w.Color("orange"), w.Size("6"),
                w.B(BlockCatalog.PenUp), w.F("50"), w.R("90"), w.B(BlockCatalog.PenDown),
                w.Rep("36", w.F("10"), w.R("10")));
            return w.P;
        }

        private static BlockProgram Constellation()
        {
            var w = new Writer(BlockView.Tiles);
            var stars = new List<Block> { w.Size("3") };
            foreach (var c in new[] { "red", "orange", "gold", "lime", "teal", "sky", "purple", "magenta" })
            {
                stars.Add(w.Color(c));
                stars.Add(w.Rep("5", w.F("60"), w.R("144")));
                stars.Add(w.R("45"));
            }
            w.Script(40, 40, BlockCatalog.Start, stars.ToArray());
            return w.P;
        }

        private static BlockProgram BubbleRain()
        {
            var w = new Writer(BlockView.Lines);
            w.Script(40, 40, BlockCatalog.Start, w.B(BlockCatalog.Background, ("color", "navy")));
            w.Script(40, 200, BlockCatalog.Forever,
                w.B(BlockCatalog.Circle, ("color", "sky"), ("size", "20"), ("place", "random")),
                w.If("coin", w.B(BlockCatalog.Circle, ("color", "magenta"), ("size", "10"), ("place", "random"))),
                w.If("coin", w.B(BlockCatalog.Circle, ("color", "gold"), ("size", "10"), ("place", "random"))));
            w.Script(40, 520, BlockCatalog.Click, w.B(BlockCatalog.Background, ("color", "navy")));
            return w.P;
        }

        private static BlockProgram MagicBrush()
        {
            var w = new Writer(BlockView.Lines);
            w.Script(40, 40, BlockCatalog.Forever,
                w.B(BlockCatalog.Circle, ("color", "sky"), ("size", "20"), ("place", "mouse")),
                w.If("mouse", w.B(BlockCatalog.Circle, ("color", "magenta"), ("size", "50"), ("place", "mouse"))));
            w.Script(40, 320, BlockCatalog.Key, w.B(BlockCatalog.Background, ("color", "white")));
            return w.P;
        }

        private static BlockProgram SteerGame()
        {
            var w = new Writer(BlockView.Lines);
            w.Script(40, 40, BlockCatalog.Start, w.Color("green"), w.Size("6"));
            w.Script(40, 220, BlockCatalog.Forever,
                w.If("up", w.F("5")),
                w.If("down", w.Back("5")),
                w.If("left", w.L("10")),
                w.If("right", w.R("10")));
            return w.P;
        }

        private static BlockProgram RockBand()
        {
            var w = new Writer(BlockView.Tiles);
            w.Script(40, 40, BlockCatalog.MusicLoop,
                w.B(BlockCatalog.Drum, ("drum", "kick")), w.B(BlockCatalog.Wait, ("beats", "0.5")),
                w.B(BlockCatalog.Drum, ("drum", "hat")), w.B(BlockCatalog.Wait, ("beats", "0.5")),
                w.B(BlockCatalog.Drum, ("drum", "snare")), w.B(BlockCatalog.Wait, ("beats", "0.5")),
                w.B(BlockCatalog.Drum, ("drum", "hat")), w.B(BlockCatalog.Wait, ("beats", "0.5")));
            w.Script(40, 240, BlockCatalog.MusicLoop,
                w.B(BlockCatalog.Instrument, ("instrument", "Square")),
                w.B(BlockCatalog.Note, ("note", "C4")), w.B(BlockCatalog.Wait, ("beats", "0.5")),
                w.B(BlockCatalog.Note, ("note", "E4")), w.B(BlockCatalog.Wait, ("beats", "0.5")),
                w.B(BlockCatalog.Note, ("note", "G4")), w.B(BlockCatalog.Wait, ("beats", "0.5")),
                w.B(BlockCatalog.Note, ("note", "C5")), w.B(BlockCatalog.Wait, ("beats", "1")));
            return w.P;
        }
    }
}
