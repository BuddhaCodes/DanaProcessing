using DanaProcessing.Ide;
using Xunit;

namespace DanaProcessing.Tests.Ide
{
    /// <summary>The rewind bar's state: usable while the sketch runs, greyed out after Stop.</summary>
    public class TimelineBarTests
    {
        private static int FrameOf(int index) => index + 1;

        [Fact]
        public void While_running_the_bar_can_pause_and_rewind()
        {
            var view = TimelineBar.Describe(count: 300, frozen: false, stopped: false, historyIndex: -1, FrameOf);

            Assert.True(view.Enabled);
            Assert.True(view.SliderEnabled);
            Assert.False(view.Frozen);
            Assert.Equal(299, view.Index); // the slider sits on the newest frame
            Assert.Contains("300", view.Label);
        }

        [Fact]
        public void Rewound_it_says_which_frame_and_how_far_back()
        {
            var view = TimelineBar.Describe(count: 300, frozen: true, stopped: false, historyIndex: 99, FrameOf);

            Assert.True(view.Enabled);
            Assert.True(view.Frozen);
            Assert.Equal(99, view.Index);
            Assert.Contains("100", view.Label); // FrameCount of that frame
            Assert.Contains("200", view.Label); // frames back
        }

        [Fact]
        public void After_Stop_the_whole_bar_is_greyed_out_and_says_why()
        {
            // Even if it was rewound when Stop came, or has frames recorded.
            foreach (bool frozen in new[] { false, true })
            {
                var view = TimelineBar.Describe(count: 300, frozen: frozen, stopped: true, historyIndex: 10, FrameOf);

                Assert.False(view.Enabled);
                Assert.False(view.SliderEnabled);
                Assert.False(view.Frozen); // no "continue from here" either
                Assert.False(string.IsNullOrWhiteSpace(view.Label));
            }
        }

        [Fact]
        public void With_nothing_recorded_yet_there_is_nothing_to_drag()
        {
            var view = TimelineBar.Describe(count: 1, frozen: false, stopped: false, historyIndex: -1, FrameOf);

            Assert.True(view.Enabled);
            Assert.False(view.SliderEnabled);
            Assert.Equal(0, view.Index);
        }
    }
}
