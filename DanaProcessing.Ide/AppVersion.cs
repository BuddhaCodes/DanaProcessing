using System.Reflection;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// The app's own version string ("0.1.0-beta"), read from the
    /// AssemblyInformationalVersionAttribute the SDK generates from
    /// DanaProcessing.Ide.csproj's &lt;Version&gt; property. Deliberately NOT
    /// FileVersionInfo.GetVersionInfo(Assembly.Location) — Assembly.Location
    /// comes back empty for a bundled assembly inside a
    /// PublishSingleFile+IncludeAllContentForSelfExtract build (the exact
    /// same pitfall ExportedSketchRunner already had to work around for its
    /// own sketch-file lookup), so this reads the version straight off the
    /// assembly's own attribute instead, which survives single-file
    /// publishing fine.
    /// </summary>
    internal static class AppVersion
    {
        // Declaration order matters here: static initializers run top to
        // bottom, and Current's own initializer reads Full — Full has to be
        // the one declared first, or Current would run against Full's
        // not-yet-initialized (null) backing field.

        /// <summary>Full informational version, including the "+&lt;git commit hash&gt;" suffix — worth logging/showing in a diagnostics or "about" context where the exact build matters.</summary>
        public static string Full { get; } =
            Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
            ?? "dev";

        /// <summary>Short display version ("0.1.0-beta") for the UI — the SDK
        /// also appends "+&lt;git commit hash&gt;" to InformationalVersion
        /// (SourceRevisionId), which is useful in a bug report but far too
        /// long for a status-bar label, so this trims everything from '+'
        /// onward.</summary>
        public static string Current { get; } = Full.Split('+')[0];
    }
}
