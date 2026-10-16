using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// What the one Run/Stop button says. It's a switch: stopped, it offers
    /// ▶ Run; while a sketch is live, the same button becomes ■ Stop with a
    /// breathing dot and how long the sketch has been running ("■ Stop · 1:42"),
    /// so the title bar always tells you whether something is playing. Pure,
    /// so it can be tested without a window.
    /// </summary>
    public static class RunToggle
    {
        /// <summary>The button's text: "▶  Run" when stopped, "■  Stop · m:ss" while running.</summary>
        public static string Label(bool running, TimeSpan elapsed) =>
            running
                ? Loc.Tr("■  Detener", "■  Stop") + " · " + FormatElapsed(elapsed)
                : Loc.Tr("▶  Ejecutar", "▶  Run");

        /// <summary>The tooltip: what a click does, and the keys for the rest.</summary>
        public static string Tooltip(bool running) =>
            running
                ? Loc.Tr("En vivo. Clic para detener el sketch y su sonido (Shift+F5) · Ctrl+R lo reinicia",
                         "Live. Click to stop the sketch and its sound (Shift+F5) · Ctrl+R restarts it")
                : Loc.Tr("Compilar y ejecutar el sketch  (Ctrl+R)", "Compile and run the sketch  (Ctrl+R)");

        /// <summary>0:07, 1:42, 12:05, then 1:02:03 past an hour.</summary>
        public static string FormatElapsed(TimeSpan t)
        {
            if (t < TimeSpan.Zero)
                t = TimeSpan.Zero;
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
        }
    }
}
