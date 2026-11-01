using System.Collections.Generic;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// What each block, category and value does, in a sentence a child can
    /// follow: shown when the mouse rests on it (icons alone don't always say).
    /// </summary>
    public static class BlockHelp
    {
        private static readonly Dictionary<string, (string Es, string En)> Blocks = new()
        {
            [BlockCatalog.Start] = ("Lo que pongas acá pasa una vez, apenas tocás Probar.", "What you put here happens once, as soon as you press Try it."),
            [BlockCatalog.Forever] = ("Lo que pongas acá se repite todo el tiempo, muchas veces por segundo.", "What you put here repeats all the time, many times a second."),
            [BlockCatalog.Click] = ("Lo que pongas acá pasa cada vez que hacés clic sobre el dibujo.", "What you put here happens every time you click on the drawing."),
            [BlockCatalog.Key] = ("Lo que pongas acá pasa cada vez que tocás una tecla.", "What you put here happens every time you press a key."),
            [BlockCatalog.MusicLoop] = ("Música que suena en bucle, sin parar. Podés tener varias a la vez.", "Music that loops without stopping. You can have several at once."),

            [BlockCatalog.Forward] = ("La tortuga camina hacia adelante. Si el lápiz está abajo, dibuja una línea.", "The turtle walks forward. If the pen is down, it draws a line."),
            [BlockCatalog.Back] = ("La tortuga camina para atrás, sin darse vuelta.", "The turtle walks backwards, without turning round."),
            [BlockCatalog.Right] = ("La tortuga gira hacia la derecha, como las agujas del reloj. 90° es una esquina.", "The turtle turns right, like a clock's hands. 90° is a corner."),
            [BlockCatalog.Left] = ("La tortuga gira hacia la izquierda. 90° es una esquina.", "The turtle turns left. 90° is a corner."),
            [BlockCatalog.PenUp] = ("La tortuga levanta el lápiz: camina sin dibujar.", "The turtle lifts its pen: it walks without drawing."),
            [BlockCatalog.PenDown] = ("La tortuga baja el lápiz: vuelve a dibujar al caminar.", "The turtle puts its pen down: it draws again as it walks."),
            [BlockCatalog.PenColor] = ("Cambia el color de lo que dibuja la tortuga desde ahora.", "Changes the color the turtle draws with from now on."),
            [BlockCatalog.PenSize] = ("Cambia qué tan gruesa es la línea de la tortuga.", "Changes how thick the turtle's line is."),
            [BlockCatalog.Home] = ("La tortuga vuelve al centro, mirando para arriba.", "The turtle goes back to the middle, facing up."),

            [BlockCatalog.Background] = ("Pinta todo el fondo de un color. Borra lo que había dibujado.", "Paints the whole background one color. It wipes what was drawn."),
            [BlockCatalog.Circle] = ("Dibuja un círculo del color, tamaño y lugar que elijas.", "Draws a circle of the color, size and place you choose."),
            [BlockCatalog.Square] = ("Dibuja un cuadrado del color, tamaño y lugar que elijas.", "Draws a square of the color, size and place you choose."),

            [BlockCatalog.Repeat] = ("Hace los bloques de adentro varias veces seguidas.", "Does the blocks inside it several times in a row."),
            [BlockCatalog.If] = ("Hace los bloques de adentro solo si pasa lo que elegiste.", "Does the blocks inside it only when what you chose is happening."),

            [BlockCatalog.Note] = ("Toca una nota musical.", "Plays a musical note."),
            [BlockCatalog.Drum] = ("Toca un tambor: el bombo, la caja o el platillo.", "Plays a drum: the kick, the snare or the hi-hat."),
            [BlockCatalog.Wait] = ("Espera un rato antes del próximo sonido (solo dentro de la música).", "Waits a moment before the next sound (only inside music)."),
            [BlockCatalog.Instrument] = ("Cambia el instrumento que tocan las notas.", "Changes the instrument the notes are played on."),

            [BlockCatalog.ScoreReset] = ("Pone los puntos en cero.", "Sets the score back to zero."),
            [BlockCatalog.ScoreAdd] = ("Suma un punto.", "Adds one point."),
            [BlockCatalog.ScoreShow] = ("Escribe los puntos arriba a la izquierda del dibujo.", "Writes the score at the top left of the drawing."),

            [BlockCatalog.VarSet] = ("Guarda un número en una variable: una cajita con nombre que recuerda un valor.", "Stores a number in a variable: a named box that remembers a value."),
            [BlockCatalog.VarChange] = ("Suma (o resta) a lo que tiene guardado la variable.", "Adds to (or takes away from) what the variable holds."),
            [BlockCatalog.VarShow] = ("Escribe en el dibujo lo que tiene guardado la variable.", "Writes what the variable holds on the drawing."),

            [BlockCatalog.Define] = ("Enseñale a la tortuga una palabra nueva: los bloques de acá son lo que significa.", "Teach the turtle a new word: the blocks here are what it means."),
            [BlockCatalog.Call] = ("Usa una palabra que le enseñaste a la tortuga con \"para hacer\".", "Uses a word you taught the turtle with \"to make\"."),
        };

        /// <summary>What a block does.</summary>
        public static string For(string type) => Blocks.TryGetValue(type, out var t) ? Loc.Tr(t.Es, t.En) : BlockCatalog.Get(type).Label;

        /// <summary>What a palette category holds.</summary>
        public static string For(BlockCategory c) => c switch
        {
            BlockCategory.Events => Loc.Tr("Empezar: cuándo pasan las cosas (al empezar, siempre, con un clic...).", "Start: when things happen (when it starts, forever, on a click...)."),
            BlockCategory.Turtle => Loc.Tr("Tortuga: una tortuga que camina, gira y dibuja.", "Turtle: a turtle that walks, turns and draws."),
            BlockCategory.Draw => Loc.Tr("Dibujar: fondos, círculos y cuadrados.", "Draw: backgrounds, circles and squares."),
            BlockCategory.Control => Loc.Tr("Repetir: hacer cosas varias veces, o solo si pasa algo.", "Repeat: do things several times, or only when something happens."),
            BlockCategory.Music => Loc.Tr("Música: notas, tambores e instrumentos.", "Music: notes, drums and instruments."),
            BlockCategory.Score => Loc.Tr("Puntos: para juegos que cuentan puntos.", "Score: for games that keep score."),
            BlockCategory.Variables => Loc.Tr("Variables: cajitas con nombre que recuerdan números.", "Variables: named boxes that remember numbers."),
            BlockCategory.MyBlocks => Loc.Tr("Inventar: palabras nuevas para la tortuga, hechas con bloques.", "Invent: new words for the turtle, made of blocks."),
            _ => BlockCatalog.CategoryName(c),
        };

        /// <summary>A value slot: what it is now, and that tapping changes it.</summary>
        public static string ForSlot(SlotKind kind, SlotChoice current) =>
            BlocksWorkspace.PickerQuestion(kind) + " " +
            Loc.Tr($"Ahora: {current.Label}. Tocá para cambiarlo.", $"Now: {current.Label}. Click to change it.");
    }
}
