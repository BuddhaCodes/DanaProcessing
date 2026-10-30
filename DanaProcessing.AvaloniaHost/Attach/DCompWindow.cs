using System.Runtime.InteropServices;
using SkiaSharp;

namespace DanaProcessing.AvaloniaHost.Attach
{
    /// <summary>
    /// The GPU presenter, the way Windows Terminal puts its own frames on
    /// screen (AtlasEngine): a DXGI flip-model swap chain with premultiplied
    /// alpha, shown through DirectComposition. Skia still draws on the CPU
    /// into its own buffer; each frame is uploaded to the swap chain's back
    /// buffer (one GPU copy) and presented in sync with the screen. Compared
    /// with UpdateLayeredWindow there's no GDI copy of the whole bitmap into
    /// DWM every frame, and the frame lands on a refresh instead of whenever.
    ///
    /// The window has no redirection bitmap (WS_EX_NOREDIRECTIONBITMAP): what
    /// it shows comes only from the composition visual. It's also layered with
    /// a fixed alpha of 255 + transparent, above its target and behind it
    /// alike (see ShouldBeLayered): above, so clicks go through to the window
    /// below; behind, because a plain (non-layered) flip-model window right
    /// under a see-through Windows Terminal made the terminal's tab bar flash
    /// transparent whenever the mouse went over it. Layered, DWM always
    /// composes it like any other window and the flash is gone.
    ///
    /// COM is called through the interfaces' vtables (no extra package). The
    /// slot numbers below follow the order of the methods in d3d11.h, dxgi.h,
    /// dxgi1_2.h and dcomp.h. Anything that fails throws: the host then falls
    /// back to LayeredWindow.
    /// </summary>
    internal sealed class DCompWindow : OverlayWindowBase, IOverlayWindow
    {
        public SKSurface? Surface { get; private set; }
        public int PixelWidth { get; private set; }
        public int PixelHeight { get; private set; }
        public string Presenter => "GPU";

        private IntPtr _device, _context, _dxgiDevice, _factory, _swapChain;
        private IntPtr _dcompDevice, _target, _visual;
        private IntPtr _pixels;
        private int _windowW, _windowH;

        // The per-frame calls, looked up once (a delegate per call would allocate every frame).
        private Com.PresentFn? _present;
        private Com.GetBuffer? _getBuffer;
        private Com.UpdateSubresource? _update;

        /// <summary>
        /// Whether the window is layered for an overlay on this layer: always.
        /// Behind a translucent terminal profile a non-layered one made the
        /// terminal's tab bar flash on hover (seen on Windows 11, 26-oct-2026);
        /// the CPU presenter, which is layered, never did.
        /// </summary>
        public static bool ShouldBeLayered(AttachLayer layer) => true;

        /// <param name="clickThrough">Layered + transparent (clicks go through). See ShouldBeLayered.</param>
        public DCompWindow(bool clickThrough = true) : base(Win32.WS_EX_NOREDIRECTIONBITMAP, layered: clickThrough)
        {
            try
            {
                // Layered with a fixed alpha: still click-through, content from the visual.
                if (clickThrough && !Win32.SetLayeredWindowAttributes(Handle, 0, 255, Win32.LWA_ALPHA))
                    throw new InvalidOperationException($"SetLayeredWindowAttributes ({Marshal.GetLastWin32Error()})");

                CreateDevice();
                Check(Com.QueryInterface(_device, Com.IID_IDXGIDevice, out _dxgiDevice), "QueryInterface(IDXGIDevice)");
                Check(Native.CreateDXGIFactory2(0, Com.IID_IDXGIFactory2, out _factory), "CreateDXGIFactory2");
                Check(Native.DCompositionCreateDevice(_dxgiDevice, Com.IID_IDCompositionDevice, out _dcompDevice), "DCompositionCreateDevice");
                Check(Com.Call<Com.CreateTargetForHwnd>(_dcompDevice, Com.Slot.DCompDevice_CreateTargetForHwnd)(_dcompDevice, Handle, 1, out _target), "CreateTargetForHwnd");
                Check(Com.Call<Com.CreateVisual>(_dcompDevice, Com.Slot.DCompDevice_CreateVisual)(_dcompDevice, out _visual), "CreateVisual");
                Check(Com.Call<Com.OnePtr>(_target, Com.Slot.DCompTarget_SetRoot)(_target, _visual), "SetRoot");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void CreateDevice()
        {
            const int Hardware = 1, Warp = 5;
            const uint BgraSupport = 0x20, SdkVersion = 7;
            int hr = Native.D3D11CreateDevice(IntPtr.Zero, Hardware, IntPtr.Zero, BgraSupport, IntPtr.Zero, 0, SdkVersion,
                                              out _device, out _, out _context);
            if (hr < 0)
                hr = Native.D3D11CreateDevice(IntPtr.Zero, Warp, IntPtr.Zero, BgraSupport, IntPtr.Zero, 0, SdkVersion,
                                              out _device, out _, out _context);
            Check(hr, "D3D11CreateDevice");
        }

        public bool EnsureSize(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            if (Surface != null && width == PixelWidth && height == PixelHeight)
                return false;

            Surface?.Dispose();
            Surface = null;
            if (_pixels != IntPtr.Zero)
                Marshal.FreeHGlobal(_pixels);
            _pixels = Marshal.AllocHGlobal(checked(width * height * 4));

            if (_swapChain == IntPtr.Zero)
            {
                var desc = new Com.SwapChainDesc1
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    Format = Com.FormatB8G8R8A8,
                    SampleCount = 1,
                    BufferUsage = Com.UsageRenderTargetOutput,
                    BufferCount = 2,
                    Scaling = 0,          // DXGI_SCALING_STRETCH (the only one composition swap chains take)
                    SwapEffect = 3,       // DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL
                    AlphaMode = 1,        // DXGI_ALPHA_MODE_PREMULTIPLIED: see-through where the sketch didn't draw
                };
                Check(Com.Call<Com.CreateSwapChainForComposition>(_factory, Com.Slot.Factory2_CreateSwapChainForComposition)(_factory, _device, ref desc, IntPtr.Zero, out _swapChain),
                      "CreateSwapChainForComposition");
                Check(Com.Call<Com.OnePtr>(_visual, Com.Slot.DCompVisual_SetContent)(_visual, _swapChain), "SetContent");
                _present = Com.Call<Com.PresentFn>(_swapChain, Com.Slot.SwapChain_Present);
                _getBuffer = Com.Call<Com.GetBuffer>(_swapChain, Com.Slot.SwapChain_GetBuffer);
                _update = Com.Call<Com.UpdateSubresource>(_context, Com.Slot.Context_UpdateSubresource);
                Check(Com.Call<Com.NoArgs>(_dcompDevice, Com.Slot.DCompDevice_Commit)(_dcompDevice), "Commit");
            }
            else
            {
                Check(Com.Call<Com.ResizeBuffers>(_swapChain, Com.Slot.SwapChain_ResizeBuffers)(_swapChain, 2, (uint)width, (uint)height, Com.FormatB8G8R8A8, 0),
                      "ResizeBuffers");
            }

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            Surface = SKSurface.Create(info, _pixels, width * 4)
                ?? throw new InvalidOperationException("SKSurface.Create");
            Surface.Canvas.Clear(SKColors.Transparent);
            PixelWidth = width;
            PixelHeight = height;
            return true;
        }

        public bool Present(int x, int y, IntPtr target, AttachLayer layer, bool pixelsChanged)
        {
            if (Surface == null || _swapChain == IntPtr.Zero)
                return true;
            try
            {
                // The window must be where the target is and as big as the swap chain.
                if (x != X || y != Y || _windowW != PixelWidth || _windowH != PixelHeight)
                {
                    Win32.SetWindowPos(Handle, IntPtr.Zero, x, y, PixelWidth, PixelHeight,
                        Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_NOOWNERZORDER);
                    X = x;
                    Y = y;
                    _windowW = PixelWidth;
                    _windowH = PixelHeight;
                }

                if (pixelsChanged || !Shown)
                {
                    Surface.Canvas.Flush();
                    // Flip model: buffer 0 is always the one to draw into next.
                    Check(_getBuffer!(_swapChain, 0, Com.IID_ID3D11Texture2D, out var texture), "GetBuffer");
                    try
                    {
                        _update!(_context, texture, 0, IntPtr.Zero, _pixels, (uint)(PixelWidth * 4), 0);
                    }
                    finally
                    {
                        Marshal.Release(texture);
                    }
                    // Sync interval 1: on the next refresh, like Terminal's own frames.
                    int hr = _present!(_swapChain, 1, 0);
                    if (hr < 0)
                        return false; // device lost and the like: the host switches to the CPU presenter
                }

                KeepZOrder(target, layer);
                ShowIfHidden();
                return true;
            }
            catch (Exception ex)
            {
                DanaLogger.ErrorFromException(ex, "DirectComposition");
                return false;
            }
        }

        public void Dispose()
        {
            Surface?.Dispose();
            Surface = null;
            foreach (var p in new[] { _visual, _target, _dcompDevice, _swapChain, _factory, _dxgiDevice, _context, _device })
                if (p != IntPtr.Zero)
                    Marshal.Release(p);
            _visual = _target = _dcompDevice = _swapChain = _factory = _dxgiDevice = _context = _device = IntPtr.Zero;
            if (_pixels != IntPtr.Zero)
                Marshal.FreeHGlobal(_pixels);
            _pixels = IntPtr.Zero;
            PixelWidth = PixelHeight = 0;
            DestroyWindow();
        }

        private static void Check(int hr, string what)
        {
            if (hr < 0)
                throw new InvalidOperationException($"{what}: 0x{hr:X8}");
        }

        private static class Native
        {
            [DllImport("d3d11.dll")]
            public static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
                IntPtr featureLevels, uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);

            [DllImport("dxgi.dll")]
            public static extern int CreateDXGIFactory2(uint flags, in Guid riid, out IntPtr factory);

            [DllImport("dcomp.dll")]
            public static extern int DCompositionCreateDevice(IntPtr dxgiDevice, in Guid iid, out IntPtr device);
        }
    }

    /// <summary>Raw COM calls through vtables, for DCompWindow. Slot numbers count IUnknown's three methods.</summary>
    internal static class Com
    {
        public static readonly Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        public static readonly Guid IID_IDXGIFactory2 = new("50c83a1c-e072-4c48-87b0-3630fa36a6d0");
        public static readonly Guid IID_IDCompositionDevice = new("c37ea93a-e7aa-450d-b16f-9746cb0407f3");
        public static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        public const uint FormatB8G8R8A8 = 87;            // DXGI_FORMAT_B8G8R8A8_UNORM
        public const uint UsageRenderTargetOutput = 0x20; // DXGI_USAGE_RENDER_TARGET_OUTPUT

        /// <summary>vtable slots (0-2 are QueryInterface, AddRef, Release).</summary>
        public static class Slot
        {
            // IDCompositionDevice: Commit, WaitForCommitCompletion, GetFrameStatistics, CreateTargetForHwnd, CreateVisual...
            public const int DCompDevice_Commit = 3;
            public const int DCompDevice_CreateTargetForHwnd = 6;
            public const int DCompDevice_CreateVisual = 7;
            // IDCompositionTarget: SetRoot
            public const int DCompTarget_SetRoot = 3;
            // IDCompositionVisual: SetOffsetX x2, SetOffsetY x2, SetTransform x2, SetTransformParent, SetEffect,
            // SetBitmapInterpolationMode, SetBorderMode, SetClip x2, SetContent
            public const int DCompVisual_SetContent = 15;
            // IDXGIObject 3-6, IDXGIFactory 7-11, IDXGIFactory1 12-13, IDXGIFactory2: IsWindowedStereoEnabled 14 ... CreateSwapChainForComposition 24
            public const int Factory2_CreateSwapChainForComposition = 24;
            // IDXGIObject 3-6, IDXGIDeviceSubObject::GetDevice 7, IDXGISwapChain: Present 8, GetBuffer 9, ..., ResizeBuffers 13
            public const int SwapChain_Present = 8;
            public const int SwapChain_GetBuffer = 9;
            public const int SwapChain_ResizeBuffers = 13;
            // ID3D11DeviceChild 3-6, then ID3D11DeviceContext's 42nd method, UpdateSubresource
            public const int Context_UpdateSubresource = 48;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SwapChainDesc1
        {
            public uint Width, Height, Format;
            public int Stereo;
            public uint SampleCount, SampleQuality;
            public uint BufferUsage, BufferCount, Scaling, SwapEffect, AlphaMode, Flags;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int NoArgs(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int OnePtr(IntPtr self, IntPtr arg);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateTargetForHwnd(IntPtr self, IntPtr hwnd, int topmost, out IntPtr target);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateVisual(IntPtr self, out IntPtr visual);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateSwapChainForComposition(IntPtr self, IntPtr device, ref SwapChainDesc1 desc, IntPtr restrictToOutput, out IntPtr swapChain);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int PresentFn(IntPtr self, uint syncInterval, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetBuffer(IntPtr self, uint buffer, in Guid riid, out IntPtr surface);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int ResizeBuffers(IntPtr self, uint count, uint width, uint height, uint format, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void UpdateSubresource(IntPtr self, IntPtr resource, uint subresource, IntPtr box, IntPtr data, uint rowPitch, uint depthPitch);

        /// <summary>The method in <paramref name="slot"/> of <paramref name="obj"/>'s vtable.</summary>
        public static T Call<T>(IntPtr obj, int slot) where T : Delegate
        {
            IntPtr vtable = Marshal.ReadIntPtr(obj);
            IntPtr fn = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer<T>(fn);
        }

        public static int QueryInterface(IntPtr obj, Guid iid, out IntPtr result) =>
            Marshal.QueryInterface(obj, in iid, out result);
    }
}
