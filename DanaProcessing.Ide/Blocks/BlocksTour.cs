using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>Where a tour step points.</summary>
    public enum TourTarget { Categories, Palette, FirstColumn, Value, CodeColumn, Indent, Run, Steps, ViewSwitch, Trash, Help }

    public sealed record TourStep(TourTarget Target, string Title, string Text);

    /// <summary>
    /// The short guided tour of each view: a few steps, each pointing at one
    /// part of the screen. Shown the first time a view is opened, and again
    /// from the "?" button. Which tours were seen is kept in the IDE's
    /// settings folder.
    /// </summary>
    public static class BlocksTour
    {
        public static IReadOnlyList<TourStep> Steps(BlockView view) => view == BlockView.Tiles
            ? new[]
            {
                new TourStep(TourTarget.Categories, Loc.Tr("Los grupos de fichas", "The groups of tiles"),
                    Loc.Tr("Cada color es un grupo: empezar, tortuga, dibujar, repetir, música... Tocá uno para ver sus fichas.",
                           "Each color is a group: start, turtle, draw, repeat, music... Click one to see its tiles.")),
                new TourStep(TourTarget.Palette, Loc.Tr("Las fichas", "The tiles"),
                    Loc.Tr("Agarrá una ficha y arrastrala hacia la derecha para usarla.", "Grab a tile and drag it to the right to use it.")),
                new TourStep(TourTarget.FirstColumn, Loc.Tr("Tu programa", "Your program"),
                    Loc.Tr("Pegá las fichas a la derecha de «al empezar», una al lado de la otra. La computadora las hace en orden, de izquierda a derecha.",
                           "Snap tiles to the right of \"when it starts\", one after another. The computer does them in order, left to right.")),
                new TourStep(TourTarget.Value, Loc.Tr("Cambiar un valor", "Changing a value"),
                    Loc.Tr("Tocá la parte de abajo de una ficha para elegir cuánto camina, cuánto gira o de qué color pinta.",
                           "Click the bottom of a tile to choose how far it walks, how much it turns or what color it paints.")),
                new TourStep(TourTarget.Run, Loc.Tr("¡Probalo!", "Try it!"),
                    Loc.Tr("Tocá Probar para ver qué hace tu programa. Si cambiás fichas mientras corre, se vuelve a probar solo.",
                           "Press Try it to see what your program does. If you change tiles while it runs, it tries again by itself.")),
                new TourStep(TourTarget.Trash, Loc.Tr("Borrar", "Deleting"),
                    Loc.Tr("Para sacar una ficha, arrastrala de vuelta a la izquierda. ¿Te equivocaste? Deshacer la trae de nuevo.",
                           "To remove a tile, drag it back to the left. Made a mistake? Undo brings it back.")),
                new TourStep(TourTarget.Help, Loc.Tr("¿Qué es esto?", "What's this?"),
                    Loc.Tr("Dejá el mouse quieto sobre cualquier ficha y te explica qué hace. Este paseo vuelve con el botón ?.",
                           "Rest the mouse on any tile and it tells you what it does. This tour comes back with the ? button.")),
            }
            : new[]
            {
                new TourStep(TourTarget.Palette, Loc.Tr("Las frases", "The sentences"),
                    Loc.Tr("Elegí un grupo a la izquierda y arrastrá una frase hacia tu programa.",
                           "Pick a group on the left and drag a sentence into your program.")),
                new TourStep(TourTarget.FirstColumn, Loc.Tr("Un renglón, una orden", "One line, one instruction"),
                    Loc.Tr("Cada renglón es una orden. La computadora las lee de arriba para abajo, como vos.",
                           "Each line is one instruction. The computer reads them top to bottom, like you do.")),
                new TourStep(TourTarget.Indent, Loc.Tr("Lo que va adentro", "What goes inside"),
                    Loc.Tr("Las frases corridas a la derecha están adentro de «repetir» o «si». Soltalas más a la derecha para meterlas adentro.",
                           "Sentences pushed to the right are inside \"repeat\" or \"if\". Drop them further right to put them inside.")),
                new TourStep(TourTarget.Value, Loc.Tr("Cambiar un valor", "Changing a value"),
                    Loc.Tr("Tocá un valor en su cajita de color (un número, un color) para elegir otro.", "Click a value in its colored box (a number, a color) to pick another one.")),
                new TourStep(TourTarget.CodeColumn, Loc.Tr("El código de verdad", "The real code"),
                    Loc.Tr("A la derecha, cada renglón en C#: lo mismo que escribiría un programador. Cuando quieras, «Pasar a código» lo abre para escribirlo vos.",
                           "On the right, each line in C#: what a programmer would type. When you're ready, \"Turn into code\" opens it for you to write.")),
                new TourStep(TourTarget.Steps, Loc.Tr("Probar y paso a paso", "Try it, step by step"),
                    Loc.Tr("Probar lo ejecuta. Con Paso a paso, la tortuga espera tu clic antes de cada renglón.",
                           "Try it runs it. With Step by step, the turtle waits for your click before each line.")),
                new TourStep(TourTarget.ViewSwitch, Loc.Tr("Fichas o renglones", "Tiles or lines"),
                    Loc.Tr("Es el mismo programa de dos formas. Cambiá cuando quieras.", "It's the same program, two ways. Switch whenever you like.")),
                new TourStep(TourTarget.Help, Loc.Tr("¿Qué hace esto?", "What does this do?"),
                    Loc.Tr("Dejá el mouse quieto sobre una frase o un valor y te lo explica. Para borrar, arrastrá a la izquierda. El paseo vuelve con ?.",
                           "Rest the mouse on a sentence or a value and it explains it. To delete, drag to the left. The tour comes back with ?.")),
            };

        internal static string FilePath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DanaProcessingIde", "blocks-tour.json");

        public static bool Seen(BlockView view) => Load().Contains(view.ToString());

        public static void MarkSeen(BlockView view)
        {
            var seen = Load();
            if (!seen.Add(view.ToString()))
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(seen));
            }
            catch (Exception)
            {
                // shown again next time: harmless
            }
        }

        private static HashSet<string> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(FilePath)) ?? new();
            }
            catch (Exception) { }
            return new HashSet<string>();
        }
    }
}
