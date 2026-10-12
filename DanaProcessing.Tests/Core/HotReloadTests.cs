using System.Runtime.CompilerServices;
using Xunit;

namespace DanaProcessing.Tests.Core;

public class HotReloadTests
{
    public class V1 : Sketch { public int Count; public float Speed = 1; public override void Draw() { } }
    public class V1Edited : Sketch { public int Count; public float Speed = 1; public override void Draw() { Count++; } }
    public class V2Changed : Sketch { public long Count; public float Speed = 1; public override void Draw() { } }
    public class V2Added : Sketch { public int Count; public float Speed = 1; public string Name = ""; public override void Draw() { } }

    private static T Bare<T>() where T : Sketch => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    [Fact]
    public void Same_fields_can_hot_reload_and_keep_state()
    {
        var old = Bare<V1>();
        old.Count = 42;
        old.Speed = 3.5f;
        var next = Bare<V1Edited>();

        var plan = SketchHotReload.Analyze(old, next);
        Assert.True(plan.CanApply, string.Join("; ", plan.Reasons));

        SketchHotReload.Transplant(old, next);
        Assert.Equal(42, next.Count);
        Assert.Equal(3.5f, next.Speed);
    }

    [Fact]
    public void Changed_field_type_blocks_hot_reload()
    {
        var plan = SketchHotReload.Analyze(Bare<V1>(), Bare<V2Changed>());
        Assert.False(plan.CanApply);
        Assert.Contains(plan.Reasons, r => r.Contains("Count") && r.Contains("type"));
    }

    [Fact]
    public void New_field_blocks_hot_reload()
    {
        var plan = SketchHotReload.Analyze(Bare<V1>(), Bare<V2Added>());
        Assert.False(plan.CanApply);
        Assert.Contains(plan.Reasons, r => r.Contains("Name"));
    }
}
