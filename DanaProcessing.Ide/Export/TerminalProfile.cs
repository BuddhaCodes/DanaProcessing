using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace DanaProcessing.Ide.Export
{
    /// <summary>
    /// "Its own terminal profile", the way Abyss Tide does it: the export adds a
    /// Windows Terminal profile (a JSON fragment) whose background is mostly
    /// see-through, and the sketch draws BEHIND the terminal. In that profile
    /// you see it through the text; in any other (opaque) profile it's hidden.
    /// install.ps1 copies the fragment to
    /// %LOCALAPPDATA%\Microsoft\Windows Terminal\Fragments\DanaSketch-&lt;id&gt;
    /// and also lists the profile in the terminal's settings.json (a one-line
    /// entry, like the ones Terminal writes itself): Terminal hides a fragment
    /// profile it has seen before but can't find there, taking it for one the
    /// user deleted (SettingsLoader::DisableDeletedProfiles).
    /// </summary>
    public static class TerminalProfile
    {
        /// <summary>The file next to the exe that install.ps1 installs as the fragment.</summary>
        public const string FragmentFileName = "terminal-profile.json";

        /// <summary>The profile's background (the color scheme's), also under the sketch.</summary>
        public const string BackgroundHex = "#0B0F17";

        /// <summary>
        /// Under the sketch, behind the 25 % terminal: mostly the scheme's
        /// background, so the desktop doesn't show through and the text reads
        /// well, but still a little see-through.
        /// </summary>
        public static SKColor DefaultBackdrop => new(0x0B, 0x0F, 0x17, 0xE0);

        /// <summary>"#RRGGBBAA", what sketch.profile holds.</summary>
        public static string BackdropHex(SKColor c) => $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}{c.Alpha:X2}";

        /// <summary>How see-through the profile is (Windows Terminal's "opacity", 0-100).</summary>
        public const int Opacity = 25;

        /// <summary>The profile's name in the terminal's new-tab menu.</summary>
        public static string ProfileName(string sketchName) => "PowerShell · " + Clean(sketchName);

        /// <summary>
        /// A GUID that only depends on the sketch's id: reinstalling (or a
        /// newer export of the same sketch) updates the same profile instead
        /// of adding another one.
        /// </summary>
        public static Guid ProfileGuid(string sketchName)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes("dana-terminal-profile:" + PowerShellInstaller.SafeId(sketchName).ToLowerInvariant()));
            var bytes = new byte[16];
            Array.Copy(hash, bytes, 16);
            return new Guid(bytes);
        }

        /// <summary>The fragment: a color scheme and one profile that uses it.</summary>
        public static string BuildFragment(string sketchName)
        {
            string name = Clean(sketchName);
            string scheme = "Dana · " + name;
            var fragment = new JsonObject
            {
                ["schemes"] = new JsonArray(new JsonObject
                {
                    ["name"] = scheme,
                    ["background"] = BackgroundHex,
                    ["foreground"] = "#D8DEE9",
                    ["cursorColor"] = "#9BE7FF",
                    ["selectionBackground"] = "#2A4A6B",
                    ["black"] = "#151A24", ["red"] = "#FF6B7F", ["green"] = "#5EE6B0", ["yellow"] = "#F2D17A",
                    ["blue"] = "#5FA8F5", ["purple"] = "#B39DFF", ["cyan"] = "#5FD7F0", ["white"] = "#D8DEE9",
                    ["brightBlack"] = "#5A6478", ["brightRed"] = "#FF8FA0", ["brightGreen"] = "#8CF5CB", ["brightYellow"] = "#FFE6A3",
                    ["brightBlue"] = "#8CC4FF", ["brightPurple"] = "#CDBEFF", ["brightCyan"] = "#9BF0FF", ["brightWhite"] = "#FFFFFF",
                }),
                ["profiles"] = new JsonArray(new JsonObject
                {
                    ["guid"] = "{" + ProfileGuid(sketchName).ToString() + "}",
                    ["name"] = ProfileName(sketchName),
                    ["commandline"] = "powershell.exe -NoLogo",
                    ["colorScheme"] = scheme,
                    ["useAcrylic"] = false,
                    ["opacity"] = Opacity,
                    ["padding"] = "12",
                    // The window takes the active tab's title: fixed here, it tells the
                    // sketch when this profile's tab is the one in front (see TitleGate).
                    ["tabTitle"] = TabTitle(sketchName),
                    ["suppressApplicationTitle"] = true,
                }),
            };
            return fragment.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }

        /// <summary>The profile's fixed tab title (and so the window's while its tab is in front).</summary>
        public static string TabTitle(string sketchName) => Clean(sketchName);

        /// <summary>
        /// Writes the fragment and the sketch.profile marker next to the exe.
        /// The marker: line 1 the backdrop ("#RRGGBBAA"), line 2 the tab title.
        /// </summary>
        public static void Write(string exportFolder, string sketchName)
        {
            File.WriteAllText(Path.Combine(exportFolder, FragmentFileName), BuildFragment(sketchName), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(exportFolder, ExportedSketchRunner.TerminalProfileMarkerFileName),
                BackdropHex(DefaultBackdrop) + "\n" + TabTitle(sketchName) + "\n", new UTF8Encoding(false));
        }

        private static string Clean(string sketchName) =>
            string.IsNullOrWhiteSpace(sketchName) ? "MiSketch" : sketchName.Trim();
    }
}
