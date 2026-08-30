using DanaProcessing;

namespace DanaProcessing.Sample
{
    /// <summary>
    /// Demonstrates: primitives (triangle), transformations (translate/rotate/
    /// push-pop matrix), keyboard input, PVector-based motion, and now the full
    /// lifecycle: FrameRate, Loop()/NoLoop(), and WindowResized().
    /// Note there's no `canvas` parameter anywhere here anymore — that's the
    /// payoff of the implicit-canvas decision.
    /// </summary>
    public class MySketch : Sketch
    {
        private PVector _pos;
        private PVector _vel;
        private float _angle;
        private bool _colorful;

        public override void Setup()
        {
            Size(600, 400);
            FrameRate(30); // try changing to e.g. 10 with the '-' key below, or 60 with '+'
            Println("Sketch inicializado correctamente.");
            _pos = new PVector(Width / 2f, Height / 2f);
            _vel = new PVector(2f, 1.5f);
        }

        public override void WindowResized()
        {
            // Width/Height are already up to date here. A real sketch might
            // reposition UI elements or recompute a layout in response.
            System.Diagnostics.Debug.WriteLine($"Ventana redimensionada a {Width}x{Height}");
        }

        public override void Draw()
        {
            Background(20, 20, 30);

            // --- Gradient-filled panel behind everything else ---
            LinearGradientFill(0, 0, Width, Height,
                Color(40, 10, 60), Color(10, 30, 60));
            NoStroke();
            Rect(0, 0, Width, 60);

            // --- HSB color cycling, tied to frame count (Processing devs love this trick) ---
            FillHSB((FrameCount * 1.5f) % 360, 80, 90);
            Text("Panel con gradiente + texto con color ciclando en HSB", 15, 35);

            // Para cargar una imagen desde disco (requiere un archivo real):
            //   var img = LoadImage("assets/logo.png");
            //   Image(img, 10, 70, 80, 80);

            // --- Circle that follows the mouse, with a trail line (from before) ---
            NoStroke();
            Fill(255, 100, 100);
            Ellipse(MouseX, MouseY, 60, 60);
            Stroke(255, 255, 255, 80);
            StrokeWeight(2);
            Line(PMouseX, PMouseY, MouseX, MouseY);

            // --- A rotating triangle, using transformations ---
            PushMatrix();
            Translate(150, 150);
            Rotate(_angle);
            NoStroke();
            Fill(100, 200, 255);
            Triangle(0, -30, 26, 20, -26, 20);
            PopMatrix();
            _angle += 1.5f;

            // --- A little particle bouncing around, driven by PVector ---
            _pos.Add(_vel);
            if (_pos.X < 0 || _pos.X > Width) _vel.X *= -1;
            if (_pos.Y < 0 || _pos.Y > Height) _vel.Y *= -1;
            Fill(_colorful ? (byte)255 : (byte)200, 220, 100);
            Ellipse(_pos.X, _pos.Y, 20, 20);

            // --- Text showing keyboard state ---
            Fill(255, 255, 255);
            TextSize(14);
            Text($"[{Width}x{Height} @ {TargetFrameRate}fps] 'L' = pausar/reanudar  |  '+/-' = frameRate  |  SPACE = color particula  |  {(IsLooping ? "corriendo" : "PAUSADO")}", 10, Height - 15);
        }

        public override void KeyPressed()
        {
            if (Key == ' ') _colorful = !_colorful;
            if (Key == 'l') { if (IsLooping) NoLoop(); else Loop(); }
            if (Key == '=' || Key == '+') FrameRate(Math.Min(TargetFrameRate + 10, 120));
            if (Key == '-') FrameRate(Math.Max(TargetFrameRate - 10, 5));

            // Demo: press 'e' to intentionally crash the sketch and see the error overlay.
            if (Key == 'e')
                throw new InvalidOperationException("Este es un error de prueba disparado a proposito presionando 'e'.");
        }
    }
}
