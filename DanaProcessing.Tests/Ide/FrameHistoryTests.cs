using DanaProcessing.AvaloniaHost;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Ide
{
    /// <summary>
    /// The rewind buffer. Before the compressed tier, a 720x520 sketch (the
    /// microphone example) kept ~85 frames -- under a second and a half, which
    /// is about how long the mouse takes to reach the rewind bar, so every
    /// frame you could scrub to looked the same.
    /// </summary>
    public class FrameHistoryTests
    {
        private static SKImage Frame(int width, int height, SKColor color)
        {
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(color);
            return surface.Snapshot();
        }

        /// <summary>A different, easy to recognise color for every frame.</summary>
        private static SKColor ColorOf(int frame) => new((byte)(frame * 37 % 256), (byte)(frame * 11 % 256), (byte)(255 - frame * 5 % 256));

        private static SKColor CenterPixel(SKImage image)
        {
            using var bitmap = SKBitmap.FromImage(image);
            return bitmap.GetPixel(image.Width / 2, image.Height / 2);
        }

        private static void AssertClose(SKColor expected, SKColor actual, int tolerance = 6)
        {
            Assert.InRange(actual.Red, expected.Red - tolerance, expected.Red + tolerance);
            Assert.InRange(actual.Green, expected.Green - tolerance, expected.Green + tolerance);
            Assert.InRange(actual.Blue, expected.Blue - tolerance, expected.Blue + tolerance);
        }

        [Fact]
        public void A_720x520_sketch_can_be_rewound_ten_seconds()
        {
            var history = new FrameHistory();
            for (int i = 1; i <= 700; i++)
            {
                history.Push(Frame(720, 520, ColorOf(i)), i);
                Assert.True(history.WaitForCompression()); // a machine that keeps up
            }

            Assert.Equal(600, history.Count); // 10 s at 60 fps (was ~85)
            Assert.Equal(101, history.FrameCountAt(0)); // the oldest 100 made room
            Assert.Equal(700, history.FrameCountAt(599));
            Assert.True(history.CompressedCount > 500, $"only {history.CompressedCount} compressed");
        }

        [Fact]
        public void Old_frames_come_back_with_their_own_picture()
        {
            var history = new FrameHistory { RawBudgetBytes = 4 * 100 * 80 * 4 }; // 4 raw frames
            for (int i = 0; i < 40; i++)
            {
                history.Push(Frame(100, 80, ColorOf(i)), i);
                history.WaitForCompression();
            }

            Assert.Equal(36, history.CompressedCount);
            foreach (int i in new[] { 0, 1, 17, 35, 36, 39 })
            {
                var image = history.Get(i);
                Assert.NotNull(image);
                Assert.Equal(100, image!.Width);
                Assert.Equal(80, image.Height);
                AssertClose(ColorOf(i), CenterPixel(image));
                Assert.Equal(i, history.FrameCountAt(i));
            }
        }

        [Fact]
        public void Frames_with_transparency_are_kept_without_losing_it()
        {
            using var clear = Frame(16, 16, SKColors.Transparent);
            using var solid = Frame(16, 16, SKColors.Orange);
            Assert.False(FrameHistory.IsOpaque(clear));
            Assert.True(FrameHistory.IsOpaque(solid));

            var png = FrameHistory.Encode(clear, 90)!;
            Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, png[..4]); // lossless, keeps alpha
            var jpeg = FrameHistory.Encode(solid, 90)!;
            Assert.Equal(new byte[] { 0xFF, 0xD8 }, jpeg[..2]);

            var history = new FrameHistory { RawBudgetBytes = 1 };
            history.Push(Frame(16, 16, SKColors.Transparent), 1);
            history.Push(Frame(16, 16, SKColors.Orange), 2);
            history.WaitForCompression();
            Assert.Equal(1, history.CompressedCount);
            Assert.Equal(0, CenterPixel(history.Get(0)!).Alpha); // not turned black
        }

        [Fact]
        public void When_compression_falls_behind_the_old_part_is_thinned_and_memory_stays_bounded()
        {
            // Never waiting for the compressor: like a big sketch on a slow machine.
            var history = new FrameHistory { RawBudgetBytes = 8 * 400 * 300 * 4, MaxEncodeBacklog = 1 };
            for (int i = 0; i < 300; i++)
                history.Push(Frame(400, 300, ColorOf(i)), i);
            history.WaitForCompression();

            Assert.InRange(history.Count, 9, 299); // some frames dropped, the newest kept
            Assert.Equal(299, history.FrameCountAt(history.Count - 1));
            int raw = history.Count - history.CompressedCount;
            Assert.InRange(raw, 1, 8 + 1 + 1); // raw budget (+ the newest, + one in flight)
            for (int i = 1; i < history.Count; i++)
                Assert.True(history.FrameCountAt(i) > history.FrameCountAt(i - 1), "frames stay in order");
            for (int i = 0; i < history.Count; i++)
                Assert.NotNull(history.Get(i));
        }

        [Fact]
        public void Continue_from_a_compressed_frame_forgets_the_future_and_Run_clears_everything()
        {
            var history = new FrameHistory { RawBudgetBytes = 2 * 32 * 32 * 4 };
            for (int i = 0; i < 20; i++)
            {
                history.Push(Frame(32, 32, ColorOf(i)), i);
                history.WaitForCompression();
            }

            history.TruncateAfter(5); // ResumeFromHere on frame 5 (already compressed)
            AssertClose(ColorOf(5), CenterPixel(history.Get(5)!));
            Assert.Equal(6, history.Count);
            history.Push(Frame(32, 32, SKColors.White), 100);
            Assert.Equal(100, history.FrameCountAt(6));

            history.Clear();
            Assert.Equal(0, history.Count);
            Assert.Null(history.Get(0));
            history.Push(Frame(32, 32, SKColors.Black), 1);
            Assert.Equal(1, history.Count);
        }
    }
}
