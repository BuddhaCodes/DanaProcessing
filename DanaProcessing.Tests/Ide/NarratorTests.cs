using System.Diagnostics;
using System.IO;
using System.Linq;
using DanaProcessing.Ide.Blocks;
using Xunit;

namespace DanaProcessing.Tests.Ide
{
    public class NarratorTests
    {
        [Fact]
        public void What_is_read_leaves_out_quotes_and_symbols()
        {
            Assert.Equal("Dibujá el mismo cuadrado, pero con el bloque repetir.",
                Narrator.Clean("Dibujá el mismo cuadrado, pero con el bloque \"repetir\"."));
            Assert.Equal("¡Lo lograste!", Narrator.Clean("¡Lo lograste! ⭐⭐⭐"));
            Assert.Equal("girá 90 grados", Narrator.Clean("girá 90°"));
        }

        [Fact]
        public void On_a_mac_a_latin_american_spanish_voice_is_preferred()
        {
            var said = new[]
            {
                "Alex                en_US    # Most people recognize me by my voice.",
                "Jorge               es_ES    # Hola, me llamo Jorge.",
                "Paulina             es_MX    # Hola, me llamo Paulina.",
                "Eddy (Spanish (Mexico)) es_MX    # ¡Hola! Me llamo Eddy.",
                "",
            };
            Assert.Equal("Paulina", Narrator.PickMacVoice(said, spanish: true));
            Assert.Equal("Alex", Narrator.PickMacVoice(said, spanish: false));
            Assert.Null(Narrator.PickMacVoice(new[] { "Thomas  fr_FR  # Bonjour" }, spanish: true));
        }

        [Fact]
        public void Speaking_starts_a_process_and_stop_ends_it()
        {
            // Never the real voice in tests: it would make noise, and its
            // audio libraries have no business in the test run.
            if (System.OperatingSystem.IsWindows())
                return;
            Narrator.Reset();
            if (!Narrator.IsAvailable)
            {
                Assert.False(Narrator.Speak("hola", spanish: true)); // no voice: quietly does nothing
                return;
            }
            Narrator.TestCommand = () =>
            {
                var info = new ProcessStartInfo("sleep");
                info.ArgumentList.Add("30");
                return info;
            };
            try
            {
                Assert.True(Narrator.Speak("Hola", spanish: true));
                Assert.True(Narrator.IsSpeaking);
                Narrator.Stop();
                Assert.False(Narrator.IsSpeaking);
                Assert.False(Narrator.Speak("   ", spanish: true));
            }
            finally
            {
                Narrator.TestCommand = null;
            }
        }
    }
}
