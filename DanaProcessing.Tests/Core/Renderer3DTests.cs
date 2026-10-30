using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// The GPU 3D renderer (RendererKind.Renderer3D): background, per-vertex colors, translucency,
/// additive light, the depth hints, live textures and 2D drawn over 3D. Each test draws into
/// a small offscreen 3D buffer and reads pixels back. On a machine with no OpenGL at all the
/// buffer can't be made and the tests have nothing to check -- and on macOS neither, since GLFW
/// only makes contexts on the main thread and tests run on worker threads (the renderer says so
/// right away instead of hanging; see the last test).
/// </summary>
[Collection("OpenGL")]
public class Renderer3DTests
{
    private sealed class Host : Sketch
    {
        public override void Draw() { }
    }

    private const int W = 80, H = 60;

    /// <summary>A 3D buffer drawn once by <paramref name="draw"/>, or null without OpenGL.</summary>
    private static PImage Render(Action<PGraphics> draw, Action<PGraphics> before = null)
    {
        PGraphics pg;
        try { pg = new Host().CreateGraphics(W, H, RendererKind.Renderer3D); }
        catch (Exception) { return null; }
        using (pg)
        {
            before?.Invoke(pg);
            pg.BeginDraw();
            draw(pg);
            pg.EndDraw();
            var img = pg.Get();
            img.LoadPixels();
            return img;
        }
    }

    /// <summary>A rectangle facing the camera at depth z (the default camera maps z = 0 to pixels).</summary>
    private static void Rect3D(PGraphics g, float x0, float y0, float x1, float y1, float z)
    {
        g.BeginShape(ShapeKind.Quads);
        g.Vertex(x0, y0, z); g.Vertex(x1, y0, z); g.Vertex(x1, y1, z); g.Vertex(x0, y1, z);
        g.EndShape();
    }

    private static void Near(Color c, int r, int g, int b, string what, int tol = 30)
    {
        Assert.True(Math.Abs(c.R - r) <= tol && Math.Abs(c.G - g) <= tol && Math.Abs(c.B - b) <= tol,
            $"{what}: expected ~({r},{g},{b}), got ({c.R},{c.G},{c.B})");
    }

    [Fact]
    public void Background_clears_a_3D_buffer_to_its_color()
    {
        var img = Render(g => g.Background(200, 40, 90));
        if (img == null) return;
        Near(img.Get(10, 10), 200, 40, 90, "background");
        Near(img.Get(W - 2, H - 2), 200, 40, 90, "background corner");
    }

    [Fact]
    public void Each_vertex_of_a_3D_mesh_keeps_its_own_fill()
    {
        var img = Render(g =>
        {
            g.Background(0);
            g.NoStroke();
            g.BeginShape(ShapeKind.Triangles);
            g.Fill(255, 0, 0); g.Vertex(0, 0, 0); g.Vertex(40, 0, 0); g.Vertex(0, H, 0);
            g.Fill(0, 0, 255); g.Vertex(40, 0, 0); g.Vertex(W, 0, 0); g.Vertex(W, H, 0);
            g.EndShape();
        });
        if (img == null) return;
        Near(img.Get(8, 10), 255, 0, 0, "red triangle");
        Near(img.Get(W - 8, 30), 0, 0, 255, "blue triangle");
    }

    [Fact]
    public void A_translucent_3D_shape_lets_what_is_behind_show_through()
    {
        var img = Render(g =>
        {
            g.Background(0);
            g.NoStroke();
            g.Fill(255, 0, 0);
            Rect3D(g, 0, 0, W, H, -20);          // far, opaque
            g.Fill(0, 0, 255, 128);
            Rect3D(g, 0, 0, W, H, 0);            // near, half see-through
        });
        if (img == null) return;
        Near(img.Get(40, 30), 127, 0, 128, "red seen through blue");
    }

    [Fact]
    public void Add_blending_sums_light_in_3D()
    {
        var img = Render(g =>
        {
            g.Background(0);
            g.NoStroke();
            g.BlendMode(BlendModeKind.Add);
            g.Fill(255, 0, 0); Rect3D(g, 0, 0, W, H, -10);
            g.Fill(0, 255, 0); Rect3D(g, 0, 0, W, H, 0);
        });
        if (img == null) return;
        Near(img.Get(40, 30), 255, 255, 0, "red + green");
    }

    [Fact]
    public void With_the_depth_mask_off_a_shape_does_not_hide_what_is_drawn_after_it()
    {
        void Scene(PGraphics g, bool mask)
        {
            g.Background(0);
            g.NoStroke();
            if (!mask) g.Hint("DISABLE_DEPTH_MASK");
            g.Fill(0, 0, 255); Rect3D(g, 0, 0, W, H, 0);       // near, drawn first
            g.Hint("ENABLE_DEPTH_MASK");
            g.Fill(255, 0, 0); Rect3D(g, 0, 0, W, H, -20);     // far, drawn after
        }
        var masked = Render(g => Scene(g, true));
        if (masked == null) return;
        Near(masked.Get(40, 30), 0, 0, 255, "the near shape hides the far one");
        var unmasked = Render(g => Scene(g, false));
        Near(unmasked.Get(40, 30), 255, 0, 0, "without the mask it hides nothing");
    }

    [Fact]
    public void Transparent_texels_neither_show_nor_hide()
    {
        // A texture that's opaque white on its left half and empty on its right, drawn near,
        // over a red shape drawn afterwards behind it: red shows through the empty half.
        var tex = new Host().CreateGraphics(20, 20);
        tex.BeginDraw(); tex.Clear(); tex.NoStroke(); tex.Fill(255); tex.Rect(0, 0, 10, 20); tex.EndDraw();
        var img = Render(g =>
        {
            g.Background(0);
            g.NoStroke();
            g.Fill(255);
            g.BeginShape(ShapeKind.Quads);
            g.Texture(tex);
            g.Vertex(0, 0, 0, 0, 0); g.Vertex(W, 0, 0, 1, 0); g.Vertex(W, H, 0, 1, 1); g.Vertex(0, H, 0, 0, 1);
            g.EndShape();
            g.Fill(255, 0, 0); Rect3D(g, 0, 0, W, H, -20);
        });
        if (img == null) return;
        Near(img.Get(15, 30), 255, 255, 255, "the opaque half");
        Near(img.Get(W - 15, 30), 255, 0, 0, "the empty half shows what's behind");
    }

    [Fact]
    public void A_live_offscreen_buffer_textures_a_3D_mesh_as_it_is_now()
    {
        PGraphics g;
        try { g = new Host().CreateGraphics(W, H, RendererKind.Renderer3D); }
        catch (Exception) { return; }
        using var __ = g;
        var page = new Host().CreateGraphics(16, 16);
        Color Frame(int r, int gr, int b)
        {
            page.BeginDraw(); page.Background(r, gr, b); page.EndDraw();
            g.BeginDraw();
            g.Background(0);
            g.NoStroke();
            g.Fill(255);
            g.BeginShape(ShapeKind.Quads);
            g.Texture(page);
            g.Vertex(0, 0, 0, 0, 0); g.Vertex(W, 0, 0, 1, 0); g.Vertex(W, H, 0, 1, 1); g.Vertex(0, H, 0, 0, 1);
            g.EndShape();
            g.EndDraw();
            var img = g.Get();
            return img.Get(40, 30);
        }
        Near(Frame(255, 0, 0), 255, 0, 0, "first frame");
        Near(Frame(0, 255, 0), 0, 255, 0, "the buffer changed, so does the mesh");
    }

    [Fact]
    public void A_textured_3D_mesh_is_tinted_by_its_vertex_fill()
    {
        var tex = new Host().CreateGraphics(8, 8);
        tex.BeginDraw(); tex.Background(255); tex.EndDraw();
        var img = Render(g =>
        {
            g.Background(0);
            g.NoStroke();
            g.Fill(255, 128, 0);
            g.BeginShape(ShapeKind.Quads);
            g.Texture(tex);
            g.Vertex(0, 0, 0, 0, 0); g.Vertex(W, 0, 0, 1, 0); g.Vertex(W, H, 0, 1, 1); g.Vertex(0, H, 0, 0, 1);
            g.EndShape();
        });
        if (img == null) return;
        Near(img.Get(40, 30), 255, 128, 0, "white texture tinted orange");
    }

    [Fact]
    public void On_macOS_a_3D_context_off_the_main_thread_fails_at_once_instead_of_hanging()
    {
        if (!OperatingSystem.IsMacOS()) return;
        // Tests run on worker threads: exactly the case that used to hang forever.
        var attempt = Task.Run(() => new Host().CreateGraphics(8, 8, RendererKind.Renderer3D));
        // Wait for it to finish either way (Wait() would rethrow the failure we're expecting).
        bool finished = ((IAsyncResult)attempt).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
        Assert.True(finished, "creating the context hung");
        Assert.True(attempt.IsFaulted, "a 3D context off the main thread should fail on macOS");
        var error = Assert.IsType<InvalidOperationException>(attempt.Exception?.InnerException);
        Assert.Contains("hilo principal", error.Message);
    }

    [Fact]
    public void Two_D_drawn_during_a_3D_frame_stays_on_top_of_it()
    {
        var img = Render(g =>
        {
            g.Background(0);
            g.NoStroke();
            g.Fill(0, 0, 255); Rect3D(g, 0, 0, W, H, 0);
            g.Fill(255, 255, 0);
            g.Rect(0, 0, 20, 20);                 // a 2D rectangle, like a label or a HUD
        });
        if (img == null) return;
        Near(img.Get(10, 10), 255, 255, 0, "2D on top");
        Near(img.Get(50, 40), 0, 0, 255, "3D elsewhere");
    }
}
