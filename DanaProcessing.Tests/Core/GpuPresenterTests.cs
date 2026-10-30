using DanaProcessing.AvaloniaHost.Attach;
using DanaProcessing.Ide.Export;
using SkiaSharp;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// The GPU presenter (DirectComposition + a DXGI swap chain, like Windows
/// Terminal's own) and the export option that turns it on.
/// </summary>
public class GpuPresenterTests
{
    /// <summary>
    /// The real thing, on Windows: creates the device, the composition target and
    /// the swap chain through their vtables, draws, uploads and presents, resizes,
    /// presents again. A wrong vtable slot would crash or fail right here, not
    /// in someone's terminal. Elsewhere it passes.
    /// </summary>
    [Fact]
    public void Presents_through_directcomposition_on_windows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        DCompWindow window;
        try { window = new DCompWindow(); }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("D3D11CreateDevice"))
        {
            return; // no Direct3D at all (a bare CI box): nothing to test
        }
        using (window)
        {
            Assert.Equal("GPU", window.Presenter);
            Assert.True(window.EnsureSize(64, 48));
            Assert.False(window.EnsureSize(64, 48));
            window.Surface!.Canvas.Clear(new SKColor(255, 0, 0, 128));
            Assert.True(window.Present(10, 10, IntPtr.Zero, AttachLayer.Above, pixelsChanged: true));
            Assert.True(window.Present(10, 10, IntPtr.Zero, AttachLayer.Above, pixelsChanged: false));

            Assert.True(window.EnsureSize(128, 96));   // ResizeBuffers
            window.Surface!.Canvas.Clear(SKColors.Transparent);
            Assert.True(window.Present(20, 20, IntPtr.Zero, AttachLayer.Above, pixelsChanged: true));
            Assert.True(window.MoveTo(30, 30));
            window.Hide();
        }

        using var behind = new DCompWindow(clickThrough: DCompWindow.ShouldBeLayered(AttachLayer.Behind));   // its own terminal profile
        Assert.True(behind.EnsureSize(32, 32));
        Assert.True(behind.Present(0, 0, IntPtr.Zero, AttachLayer.Behind, pixelsChanged: true));
    }

    /// <summary>
    /// Behind a see-through Windows Terminal a non-layered flip-model window
    /// made the tab bar flash transparent on hover; layered it doesn't.
    /// </summary>
    [Fact]
    public void Gpu_overlay_is_layered_on_both_layers()
    {
        Assert.True(DCompWindow.ShouldBeLayered(AttachLayer.Above));
        Assert.True(DCompWindow.ShouldBeLayered(AttachLayer.Behind));
    }

    [Fact]
    public void Card_shows_a_gpu_badge()
    {
        var badges = AttachedSketchFleet.StatusBadges(AttachMode.Front, 1, false, false, false, eco: true, paused: false, gpu: true);
        Assert.Equal(new[] { "ECO", "GPU" }, badges);
        Assert.DoesNotContain("GPU", AttachedSketchFleet.StatusBadges(AttachMode.Front, 1, false, false, false, false, false));
    }

    [Fact]
    public void Export_option_turns_the_gpu_on()
    {
        Assert.True(ExportedSketchRunner.BuildOverrides(false, null, null, useGpu: true).UseGpu);
        Assert.False(ExportedSketchRunner.BuildOverrides(false, null).UseGpu);
        Assert.False(AttachedRunOverrides.None.UseGpu);
        Assert.Equal("sketch.gpu", ExportedSketchRunner.GpuMarkerFileName);
    }

    [Fact]
    public void Vtable_slots_follow_the_sdk_headers()
    {
        // Guard against an accidental edit: these come from counting methods in
        // d3d11.h, dxgi.h, dxgi1_2.h and dcomp.h (IUnknown's 3 included).
        Assert.Equal((3, 6, 7, 3, 15), (Com.Slot.DCompDevice_Commit, Com.Slot.DCompDevice_CreateTargetForHwnd, Com.Slot.DCompDevice_CreateVisual,
                                        Com.Slot.DCompTarget_SetRoot, Com.Slot.DCompVisual_SetContent));
        Assert.Equal((24, 8, 9, 13, 48), (Com.Slot.Factory2_CreateSwapChainForComposition, Com.Slot.SwapChain_Present, Com.Slot.SwapChain_GetBuffer,
                                          Com.Slot.SwapChain_ResizeBuffers, Com.Slot.Context_UpdateSubresource));
        Assert.Equal(48, System.Runtime.InteropServices.Marshal.SizeOf<Com.SwapChainDesc1>());   // DXGI_SWAP_CHAIN_DESC1
    }
}
