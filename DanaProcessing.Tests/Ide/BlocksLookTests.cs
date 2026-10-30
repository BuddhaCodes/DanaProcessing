using System.Linq;
using DanaProcessing.Ide.Blocks;
using Xunit;

namespace DanaProcessing.Tests.Ide
{
    /// <summary>The bundled fonts load, and the code beside the blocks is colored correctly.</summary>
    public class BlocksLookTests
    {
        [Fact]
        public void Bundled_fonts_load_from_the_ide_itself()
        {
            Assert.Equal("Andika", BundledFonts.AndikaRegular.FamilyName);
            Assert.Equal("Andika", BundledFonts.AndikaBold.FamilyName);
            Assert.Equal("Atkinson Hyperlegible", BundledFonts.AtkinsonRegular.FamilyName);
            Assert.Equal("Atkinson Hyperlegible", BundledFonts.AtkinsonBold.FamilyName);
            Assert.StartsWith("IBM Plex Mono", BundledFonts.PlexMono.FamilyName);
            Assert.StartsWith("IBM Plex Mono", BundledFonts.PlexMonoSemiBold.FamilyName);
        }

        [Fact]
        public void Each_view_draws_with_its_own_letters()
        {
            using var tiles = new BlockStyle(BlockView.Tiles);
            using var lines = new BlockStyle(BlockView.Lines);
            Assert.Equal("Andika", tiles.Text.Typeface.FamilyName);
            Assert.Equal("Atkinson Hyperlegible", lines.Text.Typeface.FamilyName);
            Assert.StartsWith("IBM Plex Mono", lines.Mono.Typeface.FamilyName);
        }

        [Fact]
        public void Code_tokens_cover_the_whole_line_and_tell_kinds_apart()
        {
            const string line = "    for (int i = 0; i < 4; i++) { tortuga.Forward(100); Text(\"hola\", 10, 20); } // fin";
            var tokens = CSharpTokens.Split(line).ToList();

            Assert.Equal(line, string.Concat(tokens.Select(t => t.Text)));
            Assert.Contains(("for", CSharpTokens.Kind.Keyword), tokens);
            Assert.Contains(("int", CSharpTokens.Kind.Keyword), tokens);
            Assert.Contains(("Forward", CSharpTokens.Kind.Call), tokens);
            Assert.Contains(("Text", CSharpTokens.Kind.Call), tokens);
            Assert.Contains(("tortuga", CSharpTokens.Kind.Plain), tokens);
            Assert.Contains(("100", CSharpTokens.Kind.Number), tokens);
            Assert.Contains(("\"hola\"", CSharpTokens.Kind.String), tokens);
            Assert.Equal(("// fin", CSharpTokens.Kind.Comment), tokens[^1]);
        }

        [Fact]
        public void Every_value_picker_asks_a_question()
        {
            foreach (var kind in System.Enum.GetValues<SlotKind>())
                Assert.EndsWith("?", BlocksWorkspace.PickerQuestion(kind));
        }
    }
}
