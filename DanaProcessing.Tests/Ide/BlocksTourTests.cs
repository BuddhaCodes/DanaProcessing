using DanaProcessing.Ide.Blocks;
using Xunit;

namespace DanaProcessing.Tests.Ide;

public class BlocksTourTests : IDisposable
{
    private readonly string _old = BlocksTour.FilePath;
    private readonly string _file = Path.Combine(Path.GetTempPath(), "tour-" + Guid.NewGuid().ToString("N") + ".json");

    public BlocksTourTests() => BlocksTour.FilePath = _file;

    public void Dispose()
    {
        BlocksTour.FilePath = _old;
        File.Delete(_file);
    }

    [Theory]
    [InlineData(BlockView.Tiles)]
    [InlineData(BlockView.Lines)]
    public void Each_view_has_a_short_tour_that_points_at_its_own_parts(BlockView view)
    {
        var steps = BlocksTour.Steps(view);
        Assert.InRange(steps.Count, 5, 9);
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Title + s.Text)));
        Assert.Contains(steps, s => s.Target == TourTarget.Run || s.Target == TourTarget.Steps);
        if (view == BlockView.Lines)
            Assert.Contains(steps, s => s.Target == TourTarget.CodeColumn);
    }

    [Fact]
    public void A_tour_is_remembered_once_seen()
    {
        Assert.False(BlocksTour.Seen(BlockView.Tiles));
        BlocksTour.MarkSeen(BlockView.Tiles);
        Assert.True(BlocksTour.Seen(BlockView.Tiles));
        Assert.False(BlocksTour.Seen(BlockView.Lines));
    }
}
