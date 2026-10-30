namespace DanaProcessing
{
    /// <summary>
    /// The language of the text Dana itself shows outside the IDE's own
    /// windows: the attached-sketch status card, its tray menu, the speed line,
    /// host errors. Same shape as the IDE's Loc.Tr -- both variants side by side
    /// at the call site -- but here in the core, so the host and the exported
    /// apps can use it. The IDE sets it from its language setting at startup.
    /// </summary>
    public static class UiText
    {
        /// <summary>True: English. False (the default): Spanish, like the IDE.</summary>
        public static bool English { get; set; }

        public static string Tr(string es, string en) => English ? en : es;
    }
}
