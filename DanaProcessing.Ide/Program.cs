using System;
using System.Linq;
using System.Text;
using Avalonia;
using Microsoft.Win32;

namespace DanaProcessing.Ide
{
    class Program
    {
        // Scheme used by the "Test in Dana" button on the docs site
        // (danaide://run?code=<base64url-encoded UTF-8 source>). Registering
        // it under HKEY_CURRENT_USER means no admin rights and no separate
        // installer step — the app registers itself the first time it runs.
        private const string ProtocolScheme = "danaide";

        [STAThread]
        public static void Main(string[] args)
        {
            if (OperatingSystem.IsWindows())
                RegisterUrlProtocolIfNeeded();

            // Avalonia's classic desktop lifetime doesn't hand argv to App
            // itself, so stash whatever we parsed here where App can read it
            // once OnFrameworkInitializationCompleted runs.
            PendingSketch.InitialSource = TryExtractSketchSource(args);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();

        /// <summary>
        /// A registered protocol handler gets invoked as "MyApp.exe danaide://run?code=...",
        /// i.e. the whole URI arrives as a single argv entry. Pull the sketch
        /// source back out of it, or return null for a normal double-click launch.
        /// </summary>
        private static string? TryExtractSketchSource(string[] args)
        {
            var uriArg = args.FirstOrDefault(a => a.StartsWith(ProtocolScheme + "://", StringComparison.OrdinalIgnoreCase));
            if (uriArg is null || !Uri.TryCreate(uriArg, UriKind.Absolute, out var uri))
                return null;

            var codeParam = uri.Query.TrimStart('?')
                .Split('&')
                .Select(p => p.Split('=', 2))
                .FirstOrDefault(p => p.Length == 2 && p[0] == "code");

            if (codeParam is null)
                return null;

            try
            {
                return Encoding.UTF8.GetString(Base64UrlDecode(codeParam[1]));
            }
            catch
            {
                // Malformed payload (hand-edited link, truncated copy/paste,
                // browser mangling a query param) — fail quietly into a normal
                // launch rather than crash on startup over a bad URL.
                return null;
            }
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var base64 = input.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
            }
            return Convert.FromBase64String(base64);
        }

        /// <summary>
        /// Writes the "danaide" protocol under HKEY_CURRENT_USER\Software\Classes
        /// so Windows routes danaide:// links to this exe. Re-checked (and
        /// rewritten if the exe has moved) on every startup instead of once at
        /// install time, so it stays correct after e.g. an xcopy update.
        /// </summary>
        private static void RegisterUrlProtocolIfNeeded()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                    return;

                var command = $"\"{exePath}\" \"%1\"";

                using var protocolKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProtocolScheme);
                protocolKey.SetValue("", "URL:DanaProcessing IDE Sketch");
                protocolKey.SetValue("URL Protocol", "");

                using var iconKey = protocolKey.CreateSubKey("DefaultIcon");
                iconKey.SetValue("", $"\"{exePath}\",0");

                using var commandKey = protocolKey.CreateSubKey(@"shell\open\command");
                if (commandKey.GetValue("") as string != command)
                    commandKey.SetValue("", command);
            }
            catch
            {
                // No write access to HKCU (locked-down machine, policy, etc.) —
                // the app still runs fine standalone, just without the web
                // button working until this succeeds some other way.
            }
        }
    }

    /// <summary>Handoff between Program.Main's argv parsing and App, which doesn't
    /// receive argv itself under Avalonia's classic desktop lifetime.</summary>
    internal static class PendingSketch
    {
        public static string? InitialSource;
    }
}