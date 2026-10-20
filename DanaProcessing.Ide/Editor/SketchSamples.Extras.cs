using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// Samples added after 1.0 that need features from the same release
    /// (OnStop(), LoadImage(byte[]), runtimes/ support in `// nuget:`, Shade()).
    /// Kept in their own file so SketchSample.cs stays reviewable; All (in
    /// SketchSample.cs) lists these after the original catalog, followed by
    /// the ones in SketchSamples.Live.cs.
    /// </summary>
    public static partial class SketchSamples
    {
        // The 1.x additions, then the live-performance ones (SketchSamples.Live.cs).
        private static SketchSample[] Extras => ExtrasV1.Concat(Live).ToArray();

        private static readonly SketchSample[] ExtrasV1 =
        {
            new SketchSample(
                Loc.Tr("Arduino por puerto serie (System.IO.Ports)", "Arduino over serial (System.IO.Ports)"),
                Loc.Tr("La librería Serial de Processing en una línea `// nuget:`: el Arduino manda un número por línea (un potenciómetro, un sensor de luz...) y el sketch lo grafica; mantené apretado el mouse y el sketch prende el LED del Arduino. El código del Arduino está en los comentarios. Sin Arduino conectado, simula un sensor con Noise(). OnStop() cierra el puerto para que el próximo Run lo pueda abrir.",
                       "Processing's Serial library as one `// nuget:` line: the Arduino sends one number per line (a potentiometer, a light sensor...) and the sketch plots it; hold the mouse down and the sketch lights the Arduino's LED. The Arduino code is in the comments. No Arduino plugged in? It fakes a sensor with Noise(). OnStop() closes the port so the next Run can open it."),
                SerialArduino,
                new[] { SampleTag.Hardware, SampleTag.NuGet, SampleTag.Data, SampleTag.Interaction }),

            new SketchSample(
                Loc.Tr("Webcam: espejo, slit-scan y ecos (FlashCap)", "Webcam: mirror, slit-scan and echoes (FlashCap)"),
                Loc.Tr("Tu cámara con `// nuget: FlashCap` (sin dependencias nativas; Windows y Linux) y LoadImage(byte[]) para cada frame. Tecla 2, slit-scan: cada franja horizontal es un momento distinto -- arriba es ahora, abajo hace un segundo --, así que al moverte te estirás en el tiempo. 1 es un espejo, 3 deja ecos de colores. OnStop() suelta la cámara.",
                       "Your camera with `// nuget: FlashCap` (no native dependencies; Windows and Linux) and LoadImage(byte[]) for every frame. Key 2, slit-scan: every horizontal band is a different moment -- the top is now, the bottom a second ago -- so moving smears you through time. 1 is a mirror, 3 leaves colored echoes. OnStop() releases the camera."),
                WebcamSlitScan,
                new[] { SampleTag.Hardware, SampleTag.Images, SampleTag.NuGet }),

            new SketchSample(
                Loc.Tr("Pelotas que tocan notas: salida MIDI (NAudio)", "Bouncing notes: MIDI out (NAudio)"),
                Loc.Tr("Cada vez que una pelota pega en una pared suena una nota de una escala pentatónica por la salida MIDI 0 -- en Windows es el sintetizador que ya trae el sistema, así que suena sin configurar nada; con loopMIDI o un sinte USB toca tu DAW o tu hardware. Click agrega una pelota, click derecho la saca, y el panel [Tweak] cambia gravedad, nota base, instrumento y duración en vivo.",
                       "Every time a ball hits a wall, a note from a pentatonic scale plays on MIDI output 0 -- on Windows that's the synth the system already has, so it makes sound with zero setup; with loopMIDI or a USB synth it plays your DAW or hardware. Click adds a ball, right-click removes one, and the [Tweak] panel changes gravity, root note, instrument and note length live."),
                MidiBounce,
                new[] { SampleTag.Audio, SampleTag.MidiOsc, SampleTag.Hardware, SampleTag.NuGet }),

            new SketchSample(
                Loc.Tr("Shaders en vivo: plasma que escucha el ritmo (SkSL)", "Live shaders: a plasma that hears the beat (SkSL)"),
                Loc.Tr("Un fragment shader estilo Shadertoy, en SkSL, pintando cada píxel con Shade(codigo): iTime, iMouse, iAmplitude e iSpectrum[] se completan solos, y dos [LiveLoop] le dan un ritmo al que reaccionar. Cambiá un número del shader y apretá ⚡ Hot Reload (o guardá): el shader nuevo entra al instante sin cortar la música; si no compila, sigue el anterior y el error aparece abajo.",
                       "A Shadertoy-style fragment shader in SkSL painting every pixel with Shade(source): iTime, iMouse, iAmplitude and iSpectrum[] are filled in for you, and two [LiveLoop]s give it a beat to react to. Change a number in the shader and press ⚡ Hot Reload (or save): the new shader takes over instantly without stopping the music; if it doesn't compile, the old one keeps running and the error shows at the bottom."),
                LiveShader,
                new[] { SampleTag.LiveCoding, SampleTag.Audio, SampleTag.Generative, SampleTag.HotReload }),

            new SketchSample(
                Loc.Tr("Una IA aprende a escribir en ASCII", "An AI learns to write in ASCII"),
                Loc.Tr("Una red neuronal chiquita, escrita a mano en C# (sin NuGet ni descargas), aprende en vivo a dibujar la palabra que escribís: arranca en ruido y en unos segundos las letras salen del ASCII. Cada carácter es una pregunta a la red -- ¿cuánta tinta va acá? -- y brilla donde todavía está aprendiendo. Escribí otra palabra y la red desaprende una mientras aprende la otra; el mouse le dobla las entradas, la rueda muestra lo que imagina fuera de la palabra, Tab cambia los caracteres y Enter la hace olvidar todo.",
                       "A tiny neural network, written by hand in C# (no NuGet, no download), learns live to draw the word you type: it starts as noise and within seconds the letters rise out of the ASCII. Every character is one question to the network -- how much ink goes here? -- and it glows where the network is still learning. Type another word and it unlearns one while it learns the other; the mouse bends its inputs, the wheel shows what it imagines outside the word, Tab switches glyphs and Enter makes it forget everything."),
                AsciiMind,
                new[] { SampleTag.MachineLearning, SampleTag.Text, SampleTag.Generative, SampleTag.Interaction }),

            new SketchSample(
                Loc.Tr("Disco ASCII en 3D: empezás adentro del bailarín", "3D ASCII disco: you start inside the dancer"),
                Loc.Tr("Una disco hecha solo de caracteres, y cada uno tiene su X, Y y Z: un bailarín estatua de 20 unidades de alto (el torso más grueso que los brazos, redondeado, así que de costado se ve su volumen), una pista de baldosas cuyas luces suben con el bombo y una onda que sale de sus pies, dos reflectores que son haces de puntos en el aire, una bola de espejos de glifos que gira y polvo flotando a todas las alturas. La cámara arranca adentro del pecho del bailarín -- solo glifos alrededor --, sale caminando, y recién al alejarse se ve dónde estabas; después lo rodea y vuelve a entrar. El bailarín se dibuja en un PGraphics cada frame, la cámara 3D es matemática simple y la canción son cinco [LiveLoop] a 116 BPM. Espacio congela la cámara, H oculta los textos.",
                       "A disco made only of characters, each with its own X, Y and Z: a statue dancer 20 units tall (the torso thicker than the arms, rounded, so from the side you see its volume), a floor of tiles whose lights rise with the kick and a ripple running from its feet, two spotlights that are beams of dots in the air, a turning mirror ball of glyphs and dust floating at every height. The camera starts inside the dancer's chest -- just glyphs all around --, walks out, and only as it backs away do you see where you were; then it circles the dancer and dives back in. The dancer is drawn on a PGraphics every frame, the 3D camera is plain math and the song is five [LiveLoop]s at 116 BPM. Space freezes the camera, H hides the labels."),
                AsciiDisco,
                new[] { SampleTag.ThreeD, SampleTag.Text, SampleTag.Audio, SampleTag.Animation }),

            new SketchSample(
                Loc.Tr("Agujero negro en ASCII 3D, con música de película", "A 3D ASCII black hole, with a film score"),
                Loc.Tr("Cada carácter es un rayo de luz trazado hacia atrás desde la cámara y curvado por la gravedad (la ecuación de un rayo en la geometría de Schwarzschild, paso a paso). Por eso se ve la parte de atrás del disco de gas doblada por encima y por debajo de la sombra, y el lado que gira hacia vos más brillante y más blanco (efecto Doppler). La cámara llega desde lejos, baja al plano del disco y lo rodea acercándose cada vez más, y al final se aleja despacio hasta donde empezó, mientras suena una orquesta hecha con sintetizadores en ocho [LiveLoop]: cuerdas, cellos, un piano que persigue su propio eco, coro, órgano, timbales y un reloj que no para. El disco brilla más a medida que la música crece. Espacio congela la cámara, la rueda acerca o aleja, H oculta los textos.",
                       "Every character is a ray of light traced backwards from the camera and bent by gravity (the equation of a light ray in Schwarzschild geometry, stepped through). That's why you see the far side of the gas disk bent over and under the shadow, and the side spinning towards you brighter and whiter (Doppler beaming). The camera comes in from far away, sinks to the disk's plane and circles it ever closer, then slowly pulls back out to where it began, while an orchestra made of synths plays in eight [LiveLoop]s: strings, cellos, a piano chasing its own echo, choir, organ, timpani and a clock that never stops. The disk glows brighter as the music grows. Space freezes the camera, the wheel moves closer or farther, H hides the labels."),
                AsciiBlackHole,
                new[] { SampleTag.ThreeD, SampleTag.Text, SampleTag.Audio, SampleTag.Generative }),

            new SketchSample(
                Loc.Tr("Líquido en ASCII 3D: agua en una caja que podés inclinar", "3D ASCII liquid: water in a box you can tilt"),
                Loc.Tr("Unas 1.500 partículas de agua simuladas en 3D (relajación de doble densidad, el método de Clavet, Beaudoin y Poulin que usan muchos juegos): cada partícula cuenta cuántas vecinas tiene cerca; si están muy apretadas las empuja y si están muy sueltas las atrae, y eso basta para que se comporte como un líquido con superficie, olas y salpicaduras. Las vecinas se buscan con una grilla, así que el costo crece con la cantidad de partículas y no con su cuadrado. Cada partícula es un glifo en 3D: # en el fondo, ~ en la superficie, puntos y estrellas en la espuma, y más blanca cuanto más rápido va. Arrastrá para inclinar la caja, click para una salpicadura, 1 2 3 cambian de escena (represa que se rompe, una bola que cae en una pileta, dos paredes de agua que chocan), la rueda acerca y A apaga el modo automático.",
                       "About 1,500 water particles simulated in 3D (double density relaxation, the Clavet, Beaudoin and Poulin method behind many game liquids): each particle counts its close neighbours; too crowded and it pushes them away, too sparse and it pulls them in, and that alone makes it behave like a liquid, with a surface, waves and splashes. Neighbours are found with a grid, so the cost grows with the number of particles, not its square. Each particle is a glyph in 3D: # deep down, ~ on the surface, dots and stars in the spray, whiter the faster it moves. Drag to tilt the box, click for a splash, 1 2 3 switch scenes (a dam break, a ball dropping into a pool, two walls of water colliding), the wheel zooms and A turns auto mode off."),
                AsciiFluid,
                new[] { SampleTag.ThreeD, SampleTag.Text, SampleTag.Particles, SampleTag.Interaction }),

        };

        private const string AsciiFluid =
@"// A 3D liquid made of ASCII characters.
//
// About 1,500 particles of ""water"" in a glass box, simulated with particle-based
// fluid dynamics (the ""double density relaxation"" method of Clavet, Beaudoin and
// Poulin, 2005 -- the trick behind many game liquids):
//   1. every particle falls and moves on with its velocity;
//   2. it counts how crowded it is (its density, from the neighbours within one
//      unit). Too crowded: it pushes its neighbours away; too sparse: it pulls
//      them in -- that's what makes it a liquid that keeps its volume, with a
//      surface, instead of sand or gas;
//   3. the walls push it back in, and its new velocity is just where it ended
//      up minus where it started.
// Neighbours are found with a grid: each particle only looks at the 27 cells
// around it, so the cost grows with the number of particles, not its square.
//
// Each particle is drawn as one glyph in 3D, sorted far to near: crowded ones
// (deep water) are dense glyphs, lonely fast ones (spray and foam) are dots and
// stars, and the faster it moves the whiter it gets.
//
// Drag the mouse: tilt the box (the water sloshes).  Wheel: zoom.
// Click: kick the water up from below.  1: dam break   2: a ball of water drops into a pool
// 3: two walls of water collide.  Space: pause.  A: auto mode on/off (it rocks by itself and
// splashes every few seconds, until you touch it).
public class AsciiFluid : Sketch
{
    // ---- the box (units: one particle's reach, h = 1) ------------------------------------
    const float BoxX = 14, BoxY = 10, BoxZ = 7;
    const int MaxParticles = 1800;

    // ---- the fluid -----------------------------------------------------------------------
    const float Gravity = 0.010f;          // per step²
    const float Stiffness = 0.08f, NearStiffness = 0.2f;
    const float Viscosity = 0.08f;
    const int Substeps = 2;
    float restDensity = 8;

    int count;
    readonly float[] px = new float[MaxParticles], py = new float[MaxParticles], pz = new float[MaxParticles];
    readonly float[] ox = new float[MaxParticles], oy = new float[MaxParticles], oz = new float[MaxParticles];
    readonly float[] vx = new float[MaxParticles], vy = new float[MaxParticles], vz = new float[MaxParticles];
    readonly float[] density = new float[MaxParticles];

    // The neighbour grid: particles sorted by cell.
    const int CX = (int)BoxX + 1, CY = (int)BoxY + 1, CZ = (int)BoxZ + 1;
    readonly int[] cellOf = new int[MaxParticles], sorted = new int[MaxParticles];
    readonly int[] cellStart = new int[CX * CY * CZ + 1];

    // ---- the view ------------------------------------------------------------------------
    PFont mono;
    float tiltX, tiltZ, autoT, zoom = 1, orbit;
    bool paused, auto = true;
    readonly float[] sx = new float[MaxParticles + 400], sy = new float[MaxParticles + 400], ss = new float[MaxParticles + 400];
    readonly float[] depth = new float[MaxParticles + 400];
    readonly int[] order = new int[MaxParticles + 400];
    int scenario = 1;

    public override void Setup()
    {
        Size(960, 540);
        mono = CreateFont(""Consolas"", 32);
        Scenario(1);
    }

    // =====================================================================================
    // Scenarios
    // =====================================================================================
    void Scenario(int which)
    {
        scenario = which;
        count = 0;
        const float s = 0.62f;                       // spacing: about 17 neighbours each
        if (which == 1)                              // dam break: a block of water against the left wall
            Block(0.3f, 0.3f, 0.3f, 5.2f, 8.5f, BoxZ - 0.3f, s);
        else if (which == 2)                         // a shallow pool, and a ball of water above it
        {
            Block(0.3f, 0.3f, 0.3f, BoxX - 0.3f, 1.9f, BoxZ - 0.3f, s);
            Ball(BoxX / 2, 7, BoxZ / 2, 2.6f, s);
        }
        else                                         // two walls of water, one at each end
        {
            Block(0.3f, 0.3f, 0.3f, 3.4f, 8.5f, BoxZ - 0.3f, s);
            Block(BoxX - 3.4f, 0.3f, 0.3f, BoxX - 0.3f, 8.5f, BoxZ - 0.3f, s);
        }
        // The rest density is what the starting water already has: so it starts at rest.
        FindNeighbours();
        Densities();
        float sum = 0; int n = 0;
        for (int i = 0; i < count; i++)
            if (density[i] > 0) { sum += density[i]; n++; }
        restDensity = n > 0 ? sum / n * 1.05f : 8;
    }

    void Block(float x0, float y0, float z0, float x1, float y1, float z1, float s)
    {
        for (float y = y0; y < y1; y += s)
            for (float x = x0; x < x1; x += s)
                for (float z = z0; z < z1; z += s)
                    Spawn(x + Jitter(), y + Jitter(), z + Jitter());
    }

    void Ball(float cx, float cy, float cz, float r, float s)
    {
        for (float y = -r; y <= r; y += s)
            for (float x = -r; x <= r; x += s)
                for (float z = -r; z <= r; z += s)
                    if (x * x + y * y + z * z <= r * r)
                        Spawn(cx + x + Jitter(), cy + y + Jitter(), cz + z + Jitter());
    }

    float Jitter() => Random(-0.02f, 0.02f);

    void Spawn(float x, float y, float z)
    {
        if (count >= MaxParticles) return;
        px[count] = x; py[count] = y; pz[count] = z;
        vx[count] = vy[count] = vz[count] = 0;
        count++;
    }

    // =====================================================================================
    // The simulation
    // =====================================================================================
    void Step(float gx, float gy, float gz)
    {
        // 1. Gravity, viscosity (neighbours moving apart or together slow down), move on.
        for (int i = 0; i < count; i++)
        {
            vx[i] += gx; vy[i] += gy; vz[i] += gz;
        }
        Viscous();
        for (int i = 0; i < count; i++)
        {
            ox[i] = px[i]; oy[i] = py[i]; oz[i] = pz[i];
            px[i] += vx[i]; py[i] += vy[i]; pz[i] += vz[i];
        }

        // 2. Push apart where it's too crowded, pull together where it's too sparse.
        FindNeighbours();
        Relax();

        // 3. Walls, then the velocity is simply how far it actually went.
        for (int i = 0; i < count; i++)
        {
            px[i] = Math.Clamp(px[i], 0.05f, BoxX - 0.05f);
            py[i] = Math.Clamp(py[i], 0.05f, BoxY - 0.05f);
            pz[i] = Math.Clamp(pz[i], 0.05f, BoxZ - 0.05f);
            vx[i] = px[i] - ox[i]; vy[i] = py[i] - oy[i]; vz[i] = pz[i] - oz[i];
        }
    }

    void FindNeighbours()
    {
        System.Array.Clear(cellStart);
        for (int i = 0; i < count; i++)
        {
            int c = Cell(px[i], py[i], pz[i]);
            cellOf[i] = c;
            cellStart[c + 1]++;
        }
        for (int c = 0; c < CX * CY * CZ; c++) cellStart[c + 1] += cellStart[c];
        var fill = new int[CX * CY * CZ];
        for (int i = 0; i < count; i++)
        {
            int c = cellOf[i];
            sorted[cellStart[c] + fill[c]++] = i;
        }
    }

    static int Cell(float x, float y, float z)
    {
        int cx = Math.Clamp((int)x, 0, CX - 1), cy = Math.Clamp((int)y, 0, CY - 1), cz = Math.Clamp((int)z, 0, CZ - 1);
        return (cy * CZ + cz) * CX + cx;
    }

    void Densities()
    {
        for (int i = 0; i < count; i++)
        {
            float d = 0;
            int c = cellOf[i], cx = c % CX, cz = c / CX % CZ, cy = c / (CX * CZ);
            for (int y = Math.Max(0, cy - 1); y <= Math.Min(CY - 1, cy + 1); y++)
                for (int z = Math.Max(0, cz - 1); z <= Math.Min(CZ - 1, cz + 1); z++)
                    for (int x = Math.Max(0, cx - 1); x <= Math.Min(CX - 1, cx + 1); x++)
                    {
                        int cell = (y * CZ + z) * CX + x;
                        for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                        {
                            int j = sorted[k];
                            if (j == i) continue;
                            float dx = px[j] - px[i], dy = py[j] - py[i], dz = pz[j] - pz[i];
                            float r2 = dx * dx + dy * dy + dz * dz;
                            if (r2 >= 1) continue;
                            float q = 1 - MathF.Sqrt(r2);
                            d += q * q;
                        }
                    }
            density[i] = d;
        }
    }

    void Relax()
    {
        for (int i = 0; i < count; i++)
        {
            // This particle's density (and ""near density"", which keeps particles from clumping).
            float d = 0, dn = 0;
            int c = cellOf[i], cx = c % CX, cz = c / CX % CZ, cy = c / (CX * CZ);
            int y0 = Math.Max(0, cy - 1), y1 = Math.Min(CY - 1, cy + 1);
            int z0 = Math.Max(0, cz - 1), z1 = Math.Min(CZ - 1, cz + 1);
            int x0 = Math.Max(0, cx - 1), x1 = Math.Min(CX - 1, cx + 1);
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int cell = (y * CZ + z) * CX + x;
                        for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                        {
                            int j = sorted[k];
                            if (j == i) continue;
                            float dx = px[j] - px[i], dy = py[j] - py[i], dz = pz[j] - pz[i];
                            float r2 = dx * dx + dy * dy + dz * dz;
                            if (r2 >= 1) continue;
                            float q = 1 - MathF.Sqrt(r2);
                            d += q * q; dn += q * q * q;
                        }
                    }
            density[i] = d;
            float pressure = Stiffness * (d - restDensity), near = NearStiffness * dn;

            // Move the neighbours (half) and this particle (the other half), along the line between them.
            float mx = 0, my = 0, mz = 0;
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int cell = (y * CZ + z) * CX + x;
                        for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                        {
                            int j = sorted[k];
                            if (j == i) continue;
                            float dx = px[j] - px[i], dy = py[j] - py[i], dz = pz[j] - pz[i];
                            float r2 = dx * dx + dy * dy + dz * dz;
                            if (r2 >= 1 || r2 < 1e-8f) continue;
                            float r = MathF.Sqrt(r2), q = 1 - r;
                            float push = (pressure * q + near * q * q) * 0.5f / r;
                            float ddx = dx * push, ddy = dy * push, ddz = dz * push;
                            px[j] += ddx; py[j] += ddy; pz[j] += ddz;
                            mx -= ddx; my -= ddy; mz -= ddz;
                        }
                    }
            px[i] += mx; py[i] += my; pz[i] += mz;
        }
    }

    void Viscous()
    {
        // A light touch of viscosity: neighbours approaching each other lose a bit of that speed.
        for (int i = 0; i < count; i++)
        {
            int c = cellOf[i], cx = c % CX, cz = c / CX % CZ, cy = c / (CX * CZ);
            for (int y = Math.Max(0, cy - 1); y <= Math.Min(CY - 1, cy + 1); y++)
                for (int z = Math.Max(0, cz - 1); z <= Math.Min(CZ - 1, cz + 1); z++)
                    for (int x = Math.Max(0, cx - 1); x <= Math.Min(CX - 1, cx + 1); x++)
                    {
                        int cell = (y * CZ + z) * CX + x;
                        for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                        {
                            int j = sorted[k];
                            if (j <= i) continue;
                            float dx = px[j] - px[i], dy = py[j] - py[i], dz = pz[j] - pz[i];
                            float r2 = dx * dx + dy * dy + dz * dz;
                            if (r2 >= 1 || r2 < 1e-8f) continue;
                            float r = MathF.Sqrt(r2);
                            float u = ((vx[i] - vx[j]) * dx + (vy[i] - vy[j]) * dy + (vz[i] - vz[j]) * dz) / r;
                            if (u <= 0) continue;
                            float imp = (1 - r) * Viscosity * u * 0.5f / r;
                            vx[i] -= dx * imp; vy[i] -= dy * imp; vz[i] -= dz * imp;
                            vx[j] += dx * imp; vy[j] += dy * imp; vz[j] += dz * imp;
                        }
                    }
        }
    }

    /// <summary>A kick from below: the water over (x, z) jumps up, more in the middle.</summary>
    void Splash(float x, float z)
    {
        for (int i = 0; i < count; i++)
        {
            float dx = px[i] - x, dz = pz[i] - z, d2 = dx * dx + dz * dz;
            if (d2 > 9) continue;
            float k = 1 - d2 / 9;
            vy[i] += 0.45f * k;
            vx[i] += dx * 0.02f * k; vz[i] += dz * 0.02f * k;
        }
    }

    // =====================================================================================
    // Each frame
    // =====================================================================================
    public override void Draw()
    {
        // The tilt: you drag it, or it rocks by itself.
        if (auto && !paused)
        {
            autoT += 1 / 60f;
            float a = Smooth(0, 4, autoT);
            tiltZ = Lerp(tiltZ, Radians(22) * Sin(autoT * 1.1f) * a, 0.06f);
            tiltX = Lerp(tiltX, Radians(9) * Sin(autoT * 0.63f + 1) * a, 0.06f);
            // Every 7 seconds something kicks the water up from below.
            if (autoT > 5 && (int)(autoT / 7) != (int)((autoT - 1 / 60f) / 7))
                Splash(Random(2, BoxX - 2), Random(1.5f, BoxZ - 1.5f));
        }
        // Gravity in the box's own frame: the box is tilted, gravity still points down.
        float cz = Cos(tiltZ), sz = Sin(tiltZ), cx = Cos(tiltX), sx_ = Sin(tiltX);
        float gx = -Gravity * sz, gy = -Gravity * cz * cx, gz = Gravity * cz * sx_;
        if (!paused)
            for (int s = 0; s < Substeps; s++)
                Step(gx, gy, gz);

        orbit += paused ? 0 : 0.0025f;
        Background(5, 7, 16);
        Render();
        Hud();
    }

    // =====================================================================================
    // Drawing: the box (glyph edges) and every particle, projected and sorted.
    // =====================================================================================
    void Render()
    {
        // Camera on a slow orbit around the box.
        float yaw = Radians(-28) + Sin(orbit) * 0.45f, pitch = Radians(24);
        float dist = 15 / zoom;
        var eye = new PVector(Sin(yaw) * Cos(pitch) * dist, Sin(pitch) * dist + 1, -Cos(yaw) * Cos(pitch) * dist);
        var f = new PVector(-eye.X, -eye.Y + 0.5f, -eye.Z).Normalize();
        var r = f.Cross(new PVector(0, 1, 0)).Normalize();
        var u = r.Cross(f);
        float focal = Height / 2f / Tan(Radians(30));

        // Box space -> world: centered, then tilted (Z tilt, then X tilt).
        float czr = Cos(tiltZ), szr = Sin(tiltZ), cxr = Cos(tiltX), sxr = Sin(tiltX);
        PVector World(float x, float y, float z)
        {
            x -= BoxX / 2; y -= BoxY / 2; z -= BoxZ / 2;
            float x1 = x * czr - y * szr, y1 = x * szr + y * czr;      // around Z
            float y2 = y1 * cxr - z * sxr, z2 = y1 * sxr + z * cxr;    // around X
            return new PVector(x1, y2, z2);
        }

        int n = 0;
        bool Project(PVector w, int slot, float scale)
        {
            float dx = w.X - eye.X, dy = w.Y - eye.Y, dz = w.Z - eye.Z;
            float zc = dx * f.X + dy * f.Y + dz * f.Z;
            if (zc < 0.5f) return false;
            float xc = dx * r.X + dy * r.Y + dz * r.Z, yc = dx * u.X + dy * u.Y + dz * u.Z;
            sx[slot] = Width / 2f + focal * xc / zc;
            sy[slot] = Height / 2f - focal * yc / zc;
            ss[slot] = Min(focal * scale / zc, 90);
            depth[n] = -zc;
            order[n] = slot;
            n++;
            return true;
        }

        for (int i = 0; i < count; i++)
            Project(World(px[i], py[i], pz[i]), i, 0.95f);

        // The glass: its 12 edges, dotted.
        int edgeSlot = MaxParticles, e = 0;
        float[] xs = { 0, BoxX }, ys = { 0, BoxY }, zs = { 0, BoxZ };
        void Edge(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            float len = MathF.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0));
            int steps = (int)(len / 0.7f);
            for (int k = 0; k <= steps && e < 400; k++)
            {
                float t = k / (float)steps;
                if (Project(World(Lerp(x0, x1, t), Lerp(y0, y1, t), Lerp(z0, z1, t)), edgeSlot + e, 0.6f)) e++;
            }
        }
        foreach (var y in ys) foreach (var z in zs) Edge(0, y, z, BoxX, y, z);
        foreach (var x in xs) foreach (var z in zs) Edge(x, 0, z, x, BoxY, z);
        foreach (var x in xs) foreach (var y in ys) Edge(x, y, 0, x, y, BoxZ);

        System.Array.Sort(depth, order, 0, n);
        TextFont(mono, 32);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        for (int q = 0; q < n; q++)
        {
            int k = order[q];
            float z = -depth[q];
            float fog = Constrain(1.35f - z / 45, 0.35f, 1);
            float size = ss[k];
            if (size < 3) continue;
            TextSize(size < 24 ? MathF.Round(size) : MathF.Round(size / 6) * 6);   // few sizes: Skia's glyph cache hits
            if (k >= MaxParticles)
            {
                Fill(150, 170, 210, 90 * fog);
                Text(""."", sx[k], sy[k]);
                continue;
            }
            float speed = MathF.Sqrt(vx[k] * vx[k] + vy[k] * vy[k] + vz[k] * vz[k]) * Substeps;
            float crowd = density[k] / restDensity;                   // ~1 inside the water, low at the surface and in spray
            float foam = Constrain(speed * 2.2f - 0.15f, 0, 1);
            // Spray, surface, body: by how crowded it is (the surface has neighbours on one side only).
            char glyph = crowd < 0.35f ? (foam > 0.35f ? '*' : '.')
                       : crowd < 0.6f ? (foam > 0.5f ? '+' : '~')
                       : crowd < 0.78f ? 'o'
                       : foam > 0.6f ? '%' : '#';
            // Deep navy at the bottom, brighter blue towards the top, white where it's fast (foam).
            float up = Constrain(py[k] / 5f, 0, 1);
            float br = Lerp(0.45f, 1, up) * (crowd < 0.72f ? 1.15f : 1);
            float r0 = 25 * br, g0 = 95 * br, b0 = 215 * br;
            Fill(Lerp(r0, 235, foam) * fog, Lerp(g0, 248, foam) * fog, Lerp(b0, 255, foam) * fog, 240);
            Text(glyph.ToString(), sx[k], sy[k]);
        }
    }

    void Hud()
    {
        TextFont(mono, 13);
        TextSize(13);
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        string[] names = { """", ""dam break"", ""drop into a pool"", ""two walls collide"" };
        Fill(255, 255, 255, 110);
        Text($""{count:N0} particles · {names[scenario]}{(paused ? "" · paused"" : """")}{(auto ? "" · auto"" : """")}"", 16, Height - 30);
        Fill(255, 255, 255, 60);
        Text(""drag: tilt · click: splash · wheel: zoom · 1 2 3: scenarios · Space: pause · A: auto"", 16, Height - 12);
    }

    public override void MouseDragged()
    {
        auto = false;
        tiltZ = Constrain(tiltZ - (MouseX - PMouseX) * 0.004f, Radians(-40), Radians(40));
        tiltX = Constrain(tiltX + (MouseY - PMouseY) * 0.004f, Radians(-30), Radians(30));
    }

    public override void MouseClicked() => Splash(Random(2, BoxX - 2), Random(1.5f, BoxZ - 1.5f));

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1 / 1.1f : 1.1f), 0.6f, 2.5f);

    public override void KeyPressed()
    {
        if (Key >= '1' && Key <= '3') { Scenario(Key - '0'); autoT = 0; }
        else if (Key == ' ') paused = !paused;
        else if (Key == 'a' || Key == 'A') auto = !auto;
    }

    static float Smooth(float a, float b, float x)
    {
        float k = Math.Clamp((x - a) / (b - a), 0, 1);
        return k * k * (3 - 2 * k);
    }
}
";

        private const string AsciiBlackHole =
@"// A black hole in ASCII, ray traced in 3D, with a slow cinematic score.
//
// Every character on screen is a ray of light traced backwards from the
// camera. Near the black hole the rays bend: in Schwarzschild's geometry a
// light ray's path obeys  p'' = -1.5 h² p / |p|^5  (h = how much it swings
// around the hole), so we just step that equation. Then, for each ray:
// - it falls in (r < 1, the event horizon): black, no character;
// - it crosses the disk of hot gas around the hole: that glow, brighter on
//   the side spinning towards us (Doppler beaming), hotter near the middle;
// - it escapes: whatever star lies in the direction it ended up going.
// The bending is why you see the disk's far side arching OVER and UNDER the
// shadow: light from behind the hole is bent around it towards you.
//
// The camera drifts in from far away, sinks to the plane of the disk and
// circles it, while an orchestra made of synths plays: strings, cellos, a
// piano figure chasing its own echo, choir, pipe organ, timpani and a clock
// that never stops ticking -- eight [LiveLoop]s, 32 bars at 76 BPM, looping.
// The disk glows brighter as the music grows.
//
// Space: freeze the camera.  H: hide the labels.  Wheel: closer / farther.
public class AsciiBlackHole : Sketch
{
    // ---- the screen -------------------------------------------------------------
    const int Cols = 120, Rows = 36;
    const float CellW = 8, CellH = 15;
    const string Ramp = "" .,:;-~=+*xX#%&@$"";       // the gas, faint to blazing
    const string StarGlyphs = "".+*"";

    PFont mono;
    readonly float[] glow = new float[Cols * Rows];   // 0..1 brightness of each ray
    readonly float[] heat = new float[Cols * Rows];   // 0 = star / cool gas, 1 = hottest gas
    readonly float[] blue = new float[Cols * Rows];   // Doppler: >0 coming at us, <0 going away

    // ---- the physics (units: the event horizon has radius 1) ------------------------
    const float DiskIn = 3, DiskOut = 11;             // the disk, from the innermost stable orbit outward
    float[,] swirl;                                   // the gas texture: [radius bin, angle bin]
    const int SwirlR = 64, SwirlA = 256;

    // ---- the camera ---------------------------------------------------------------------
    double musicStart = -1;
    bool heardMusic, frozen, labels = true;
    float frozenFor, zoomOut = 1;
    double lastBeat;

    public override void Setup()
    {
        Size(960, 540);
        Bpm = 76;
        mono = CreateFont(""Consolas"", 15);

        // Gas: arcs of brighter and darker gas. Noise sampled around circles, so it wraps
        // seamlessly; the angle is stretched so the clumps come out as long streaks.
        swirl = new float[SwirlR, SwirlA];
        for (int ri = 0; ri < SwirlR; ri++)
            for (int ai = 0; ai < SwirlA; ai++)
            {
                float a = ai * TWO_PI / SwirlA, r = ri / (float)SwirlR;
                float n = Noise(r * 14 + Cos(a) * 1.6f, Sin(a) * 1.6f + r * 3) * 0.7f
                        + Noise(r * 40 + Cos(a) * 4, Sin(a) * 4) * 0.3f;
                swirl[ri, ai] = Constrain((n - 0.3f) * 2.2f, 0, 1);
            }
    }

    public override void OnNote(NoteEvent e) => heardMusic = true;

    double SongBeat()
    {
        double b = Beat();
        if (heardMusic && musicStart < 0)
            musicStart = Math.Floor(b / 4 + 1e-6) * 4;
        if (musicStart < 0)
            return Millis() > 2500 ? b : 0;
        return Math.Max(0, b - musicStart);
    }

    public override void Draw()
    {
        double song = SongBeat();
        if (frozen) frozenFor += (float)(song - lastBeat);
        lastBeat = song;
        float camBeat = (float)song - frozenFor;
        float t = Millis() / 1000f;

        var (eye, look) = Camera(camBeat);
        Trace(eye, look, t, FrameCount % 2);             // half the rows each frame: 30 full pictures a second

        Background(0);
        TextFont(mono, 15);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        float swell = Swell((float)song);
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                int i = r * Cols + c;
                float v = glow[i];
                if (v < 0.03f) continue;
                float h = heat[i];
                char glyph;
                if (h <= 0)
                {
                    glyph = StarGlyphs[(int)Constrain(v * StarGlyphs.Length, 0, StarGlyphs.Length - 1)];
                    Fill(190, 205, 255, 255 * Min(1, v * 1.5f + 0.2f));    // a star
                }
                else
                {
                    // Gas: deep red outside, orange, then white-hot inside; the side coming at us bluer-white.
                    float d = blue[i];
                    float red = 255, green = Constrain(70 + 200 * h + 60 * d, 40, 255), bl = Constrain(20 + 230 * h * h + 120 * d, 10, 255);
                    float k = 0.45f + 0.55f * Min(1, v * (1.2f + 0.5f * swell));
                    Fill(red * k, green * k, bl * k);
                    glyph = Ramp[(int)Constrain(Sqrt(v) * Ramp.Length, 1, Ramp.Length - 1)];
                }
                Text(glyph.ToString(), (c + 0.5f) * CellW, (r + 0.5f) * CellH);
            }

        if (labels) Labels(song, eye);
    }

    // =================================================================================
    // Ray tracing
    // =================================================================================
    void Trace(PVector eye, PVector look, float t, int parity)
    {
        var f = PVector.Sub(look, eye).Normalize();
        var right = f.Cross(new PVector(0, 1, 0)).Normalize();
        var up = right.Cross(f);
        float tanHalf = Tan(Radians(50) / 2);
        float aspect = Cols * CellW / (Rows * CellH);

        for (int r = parity; r < Rows; r += 2)
            for (int c = 0; c < Cols; c++)
            {
                float sx = ((c + 0.5f) / Cols * 2 - 1) * tanHalf * aspect;
                float sy = (1 - (r + 0.5f) / Rows * 2) * tanHalf;
                float dx = f.X + right.X * sx + up.X * sy, dy = f.Y + right.Y * sx + up.Y * sy, dz = f.Z + right.Z * sx + up.Z * sy;
                float dl = Sqrt(dx * dx + dy * dy + dz * dz);
                dx /= dl; dy /= dl; dz /= dl;
                Ray(eye.X, eye.Y, eye.Z, dx, dy, dz, t, r * Cols + c);
            }
    }

    void Ray(float px, float py, float pz, float vx, float vy, float vz, float t, int i)
    {
        // h² = |p x v|²: constant along the ray; it sets how hard the hole pulls it sideways.
        float cx = py * vz - pz * vy, cy = pz * vx - px * vz, cz = px * vy - py * vx;
        float h2 = cx * cx + cy * cy + cz * cz;
        float light = 0, hot = 0, shift = 0, seen = 1;  // seen: how much of what's behind still gets through
        float start = Sqrt(px * px + py * py + pz * pz);

        // Far from the hole the ray is a straight line: skip the stepping.
        bool straight = h2 > 22 * 22;
        for (int step = 0; step < (straight ? 1 : 260); step++)
        {
            float r2 = px * px + py * py + pz * pz, rr = Sqrt(r2);
            float dt = straight ? 1e6f : Constrain(0.08f * rr, 0.04f, 1.2f);
            if (straight)
            {
                // Straight line: does it cross the disk plane ahead of us?
                if (vy != 0 && py * vy < 0)
                {
                    float k = -py / vy;
                    Disk(px + vx * k, pz + vz * k, vx, vy, vz, t, ref light, ref hot, ref shift, ref seen);
                }
                break;
            }
            if (rr < 1) { seen = 0; break; }                                  // fell in
            if (rr > start + 2 && px * vx + py * vy + pz * vz > 0) break;       // on its way out

            float a = -1.5f * h2 / (r2 * r2 * rr);
            vx += px * a * dt; vy += py * a * dt; vz += pz * a * dt;
            float nx = px + vx * dt, ny = py + vy * dt, nz = pz + vz * dt;
            if (py * ny < 0)                                                    // crossed the disk plane
            {
                float k = py / (py - ny);
                Disk(px + (nx - px) * k, pz + (nz - pz) * k, vx, vy, vz, t, ref light, ref hot, ref shift, ref seen);
                if (seen < 0.02f) break;
            }
            px = nx; py = ny; pz = nz;
        }

        if (seen > 0.02f)
        {
            // Escaped: a star, if there's one in that direction.
            float vl = Sqrt(vx * vx + vy * vy + vz * vz);
            // The sky is cut in tiny cells (~a third of a character); a few of them hold a star.
            int sx = (int)Math.Floor(vx / vl * 400), sy = (int)Math.Floor(vy / vl * 400), sz = (int)Math.Floor(vz / vl * 400);
            uint hsh = (uint)(sx * 73856093 ^ sy * 19349663 ^ sz * 83492791);
            hsh ^= hsh >> 13; hsh *= 0x5bd1e995; hsh ^= hsh >> 15;
            float s = (hsh & 0xffff) / 65535f;
            if (s > 0.9965f)
                light += seen * (0.25f + (s - 0.9965f) / 0.0035f * 0.75f);
        }
        glow[i] = Min(1, light);
        heat[i] = hot;
        blue[i] = shift;
    }

    /// <summary>A ray going through the disk at (x, 0, z): adds the gas' glow, and the gas hides what's behind it.</summary>
    void Disk(float x, float z, float vx, float vy, float vz, float t, ref float light, ref float hot, ref float shift, ref float seen)
    {
        float r = Sqrt(x * x + z * z);
        if (r < DiskIn || r > DiskOut) return;
        // The gas orbits: inner rings faster (Kepler), so the streaks wind up over time.
        float omega = 0.45f / (r * Sqrt(r));
        float angle = Atan2(z, x) - omega * t * 6;
        int ai = (int)((angle / TWO_PI % 1 + 1) % 1 * SwirlA) % SwirlA;
        int ri = (int)Constrain((r - DiskIn) / (DiskOut - DiskIn) * SwirlR, 0, SwirlR - 1);
        float edge = Smooth(DiskIn, DiskIn + 0.6f, r) * (1 - Smooth(DiskOut - 3, DiskOut, r));
        float temp = Pow(DiskIn / r, 1.6f);                                   // hotter inside
        // Doppler: the orbital velocity (counterclockwise seen from above) against the
        // direction to the camera (-v). Coming at us: brighter and bluer.
        float speed = Sqrt(0.5f / r);
        float tx = -z / r, tz = x / r;
        float vl = Sqrt(vx * vx + vy * vy + vz * vz);
        float towards = -(tx * vx + tz * vz) / vl * speed;                     // -0.4 .. 0.4
        float beam = Pow(1 / Max(0.2f, 1 - towards), 3);
        float gas = edge * (0.35f + 0.65f * swirl[ri, ai]);
        float add = seen * gas * temp * beam * 1.5f;
        light += add;
        if (add > 0.02f && hot == 0) { hot = Constrain(temp * beam * 0.8f, 0.05f, 1); shift = Constrain(towards * 2.5f, -1, 1); }
        seen *= 1 - Constrain(gas * 0.9f, 0, 0.92f);
    }

    static float Smooth(float a, float b, float x)
    {
        float k = Math.Clamp((x - a) / (b - a), 0, 1);
        return k * k * (3 - 2 * k);
    }

    // =================================================================================
    // The camera: one continuous move per pass of the song, ending where it began
    // (same place, standing still), so the loop never jumps.
    // =================================================================================
    (PVector, PVector) Camera(float beat)
    {
        float b = beat % 128;                                       // the 32-bar song
        // In from far away, then a slow creep closer all through the build and the horizon...
        float dist = Lerp(44, 14, Smooth(0, 40, b)) - 4.5f * Smooth(36, 108, b);
        // Tilt above the disk: high at first, almost edge-on by the climax (the classic
        // view, the far side of the disk bent over the top).
        float tilt = Lerp(28, 4, Smooth(4, 40, b));
        // ...and in ""after"", a long pull back up and out to where it started.
        float leave = Smooth(108, 128, b);
        dist = Lerp(dist, 44, leave) * zoomOut;
        tilt = Lerp(tilt, 28, leave);
        float around = Radians(-20 + beat * 1.4f);                  // an orbit that never stops
        float el = Radians(tilt);
        var eye = new PVector(Cos(around) * Cos(el) * dist, Sin(el) * dist, Sin(around) * Cos(el) * dist);
        return (eye, new PVector(0, 0, 0));
    }

    /// <summary>How loud the orchestra is in this bar, 0..1: the disk glows with it.</summary>
    static float Dynamics(int bar) =>
        bar < 4 ? 0.15f :
        bar < 12 ? 0.4f :
        bar < 16 ? 0.4f + 0.6f * (bar - 11) / 4f :     // the build
        bar < 28 ? 1f :
        0.4f - 0.1f * (bar - 28);                       // after

    float Swell(float beat)
    {
        int bar = (int)(beat / 4) % 32;
        // In the climax the disk also throbs with the timpani's heartbeat.
        return Dynamics(bar) + (bar >= 16 && bar < 28 ? Pulse(4, 4) * 0.5f : 0);
    }

    void Labels(double song, PVector eye)
    {
        int bar = (int)(song / 4) % 32;
        string part = bar < 4 ? ""the void"" : bar < 12 ? ""the pull"" : bar < 16 ? ""the build"" : bar < 28 ? ""the horizon"" : ""after"";
        float d = Sqrt(eye.X * eye.X + eye.Y * eye.Y + eye.Z * eye.Z);
        TextFont(mono, 13);
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 100);
        Text($""bar {bar + 1}/32 · {part} · distance {d:0.0} horizon radii{(frozen ? "" · camera frozen"" : """")}"", 14, Height - 26);
        Fill(255, 255, 255, 55);
        Text(""Space: freeze camera · wheel: closer/farther · H: hide labels"", 14, Height - 9);
    }

    public override void KeyPressed()
    {
        if (Key == ' ') frozen = !frozen;
        else if (Key == 'h' || Key == 'H') labels = !labels;
    }

    public override void MouseWheel(float delta) => zoomOut = Constrain(zoomOut * (delta > 0 ? 1.1f : 1 / 1.1f), 0.6f, 2.5f);

    // =================================================================================
    // The score: 76 BPM, 32 bars in A minor, looping. An orchestra made of synths:
    //   bars 0-3    the void:    a clock, a low drone, a high shimmer
    //   bars 4-11   the pull:    a piano figure (with its echo) and soft strings
    //   bars 12-15  the build:   strings and cellos grow, timpani roll, a rising wind
    //   bars 16-27  the horizon: everything -- organ, choir, timpani like a heartbeat
    //   bars 28-31  after:       one last chord, the piano alone, the clock
    // Each chord lasts two bars: Am F C G | Dm F Esus4 E.
    // =================================================================================
    static readonly string[][] Harmony =
    {
        new[] { ""A2"", ""E3"", ""A3"", ""C4"", ""E4"" },     // Am
        new[] { ""F2"", ""C3"", ""A3"", ""C4"", ""F4"" },     // F
        new[] { ""C3"", ""G3"", ""C4"", ""E4"", ""G4"" },     // C
        new[] { ""G2"", ""D3"", ""B3"", ""D4"", ""G4"" },     // G
        new[] { ""D3"", ""A3"", ""D4"", ""F4"", ""A4"" },     // Dm
        new[] { ""F2"", ""C3"", ""A3"", ""C4"", ""F4"" },     // F
        new[] { ""E2"", ""B2"", ""E3"", ""A3"", ""B3"" },     // Esus4: holds its breath...
        new[] { ""E2"", ""B2"", ""G#3"", ""B3"", ""E4"" },    // E: ...and leans back home
    };

    static string[] ChordAt(int bar) => Harmony[(bar / 2) % Harmony.Length];

    int clockBar, droneBar, stringBar, celloBar, pianoBar, choirBar, organBar, timpaniBar;

    [LiveLoop]
    async Task Clock()
    {
        int bar = clockBar++ % 32;
        // The tick that never stops; it steps back a little when the orchestra is loud.
        double soft = bar >= 16 && bar < 28 ? 0.6 : 1;
        for (int i = 0; i < 8; i++)
        {
            Sample(""hat"", amp: (i % 2 == 0 ? 0.20 : 0.08) * soft, rate: 1.6, pan: -0.3);
            await Sleep(0.5);
        }
    }

    [LiveLoop]
    async Task Drone()
    {
        int bar = droneBar++ % 32;
        if (bar % 2 == 0)
        {
            double root = Notes.Parse(ChordAt(bar)[0]) - (bar >= 16 && bar < 28 ? 12 : 0);
            Play(root, amp: bar < 4 ? 0.15 : bar >= 16 && bar < 28 ? 0.26 : 0.2, attack: 2, sustain: 4, release: 3, synth: Synth.Sine);
            if (bar < 4)   // the void: a faint shimmer of high harmonics
                foreach (int up in new[] { 31, 36, 40 })
                    Play(root + up, amp: 0.025, attack: 3, sustain: 2, release: 3, pan: up % 2 == 0 ? 0.5 : -0.5, synth: Synth.Triangle);
        }
        await Sleep(4);
    }

    [LiveLoop]
    async Task Strings()
    {
        int bar = stringBar++ % 32;
        float dyn = Dynamics(bar);
        if (bar >= 4 && bar % 2 == 0 && bar < 30)
        {
            // A section: each note played by two saws a hair out of tune with each other,
            // so they beat slowly against each other like a row of violins. Louder = brighter.
            var chord = ChordAt(bar);
            bool last = bar == 28;                  // the last chord rings out
            for (int k = 1; k < chord.Length; k++)
            {
                double note = Notes.Parse(chord[k]) + 12;
                foreach (double detune in new[] { -0.08, 0.08 })
                    Play(note + detune, amp: 0.012 + 0.05 * dyn, attack: 2, sustain: last ? 8 : 5, release: last ? 6 : 3,
                         cutoff: 72 + 22 * dyn, pan: detune * 4, synth: Synth.Saw);
            }
        }
        await Sleep(4);
    }

    [LiveLoop]
    async Task Cellos()
    {
        int bar = celloBar++ % 32;
        // Half notes that walk under the strings: root, fifth, third, root.
        var chord = ChordAt(bar);
        // chord[0] is the root, chord[1] the fifth, chord[3] the third (an octave down here).
        double[] walk = bar % 2 == 0
            ? new double[] { Notes.Parse(chord[0]), Notes.Parse(chord[1]) }
            : new double[] { Notes.Parse(chord[3]) - 12, Notes.Parse(chord[0]) };
        foreach (double note in walk)
        {
            if (bar >= 8 && bar < 29)
            {
                float dyn = Dynamics(bar);
                Play(note, amp: 0.10 + 0.10 * dyn, attack: 0.25, sustain: 1.4, release: 0.6, cutoff: 66 + 14 * dyn, synth: Synth.Saw);
            }
            await Sleep(2);
        }
    }

    [LiveLoop]
    async Task Piano()
    {
        int bar = pianoBar++ % 32;
        // Eight notes a bar, rising and falling through the chord, and an echo three
        // sixteenths behind on the other side -- the figure seems to chase itself.
        var chord = ChordAt(bar);
        int[] shape = { 2, 3, 4, 3, 2, 4, 3, 4 };
        bool plays = bar >= 4 && bar != 15;      // one bar of silence before the horizon
        double amp = bar >= 16 && bar < 28 ? 0.12 : 0.15;
        for (int s = 0; s < 16; s++)
        {
            if (plays)
            {
                if (s % 2 == 0)
                    Play(Notes.Parse(chord[shape[s / 2]]) + 12, amp: amp, release: 1.2, pan: 0.25, synth: Synth.Pluck);
                if (s >= 3 && (s - 3) % 2 == 0)
                    Play(Notes.Parse(chord[shape[(s - 3) / 2]]) + 12, amp: amp * 0.35, release: 1.0, pan: -0.45, synth: Synth.Pluck);
            }
            await Sleep(0.25);
        }
    }

    [LiveLoop]
    async Task Choir()
    {
        int bar = choirBar++ % 32;
        // High voices on the third and fifth, only at the horizon.
        if (bar >= 16 && bar < 28 && bar % 2 == 0)
        {
            var chord = ChordAt(bar);
            foreach (int k in new[] { 3, 4 })
            {
                double note = Notes.Parse(chord[k]) + 12;
                Play(note, amp: 0.05, attack: 2.5, sustain: 4, release: 2.5, pan: k == 3 ? -0.3 : 0.3, synth: Synth.Triangle);
                Play(note + 0.05, amp: 0.035, attack: 3, sustain: 3.5, release: 2.5, pan: k == 3 ? 0.3 : -0.3, synth: Synth.Sine);
            }
        }
        await Sleep(4);
    }

    [LiveLoop]
    async Task Organ()
    {
        int bar = organBar++ % 32;
        // The pipe organ: squares in octaves, slow to open, at the horizon only.
        if (bar >= 16 && bar < 28 && bar % 2 == 0)
            foreach (var note in ChordAt(bar))
            {
                Play(note, amp: 0.055, attack: 1.2, sustain: 5, release: 2.5, cutoff: 84, synth: Synth.Square);
                Play(Notes.Parse(note) + 12, amp: 0.03, attack: 1.6, sustain: 4.5, release: 2.5, cutoff: 90, synth: Synth.Square);
            }
        // The final chord: everything rings out over A.
        if (bar == 28)
            foreach (var note in Harmony[0])
                Play(note, amp: 0.05, attack: 0.3, sustain: 4, release: 8, cutoff: 80, synth: Synth.Square);
        await Sleep(4);
    }

    [LiveLoop]
    async Task Timpani()
    {
        int bar = timpaniBar++ % 32;
        if (bar >= 12 && bar < 16)
        {
            // The build: a roll that gets faster and louder, with a wind rising under it in the last bar.
            if (bar == 15)
                Play(48, amp: 0.10, attack: 3.8, release: 0.3, cutoff: 96, synth: Synth.Noise);
            int perBeat = bar < 14 ? 2 : bar == 14 ? 4 : 6;
            for (int i = 0; i < 4 * perBeat; i++)
            {
                double grow = (bar - 12 + i / (4.0 * perBeat)) / 4;
                Sample(""kick"", amp: 0.15 + 0.45 * grow, rate: 0.55);
                await Sleep(1.0 / perBeat);
            }
        }
        else if (bar >= 16 && bar < 28)
        {
            // The horizon: a heartbeat, and a big hit at the start of every chord.
            Sample(""kick"", amp: bar % 2 == 0 ? 0.9 : 0.6, rate: 0.55);
            await Sleep(0.5);
            Sample(""kick"", amp: 0.35, rate: 0.6);
            await Sleep(3.5);
        }
        else if (bar == 28)
        {
            Sample(""kick"", amp: 0.9, rate: 0.5);     // the last chord lands
            await Sleep(4);
        }
        else
            await Sleep(4);
    }
}
";

        private const string AsciiDisco =
@"// A disco in 3D, built only from ASCII characters, and you start INSIDE the dancer.
//
// Every character has its own X, Y and Z:
// - the dancer is a statue of glyphs, 20 units tall, standing in the middle of
//   the room. Its body is thicker than its arms (more layers in Z), so from the
//   side you see it has volume;
// - the floor is a grid of glyph tiles on the ground; the lit ones rise up with
//   the kick, and a ripple runs out from the dancer's feet;
// - two spotlights are beams of dots hanging in the air, a mirror ball of glyphs
//   turns above, and dust floats everywhere at every height.
// The camera starts inside the statue's chest -- just glyphs all around -- looks
// around, walks out, and only when it backs away do you see what you were in.
// Then it circles the dancer and dives back in.
//
// Everything is DanaProcessing and code:
// - the dancer is drawn each frame on an offscreen PGraphics (lines and circles
//   posed from Beat()), plus a second one in grays that says how thick each part is;
// - the 3D is a tiny camera in plain math: every glyph is projected, sorted far
//   to near and drawn with Text() at the size its distance gives it;
// - the music is five [LiveLoop]s (pad, bass, lead, drums, hats) at 116 BPM,
//   32 bars that loop; the camera path is timed in beats of that song.
//
// Space: freeze the camera (the music goes on).  H: hide the labels.
public class AsciiDisco : Sketch
{
    // ---- the dancer: pictures -> statue -------------------------------------------------
    const int GW = 96, GH = 54;                 // statue cells across and up (from 2x2 pixels each)
    const int PicW = GW * 2, PicH = GH * 2;
    const float Cell = 0.42f;                   // world size of one statue cell
    const string Glyphs = "" .:-=+*#%@"";
    PGraphics pic, thick;
    readonly float[] lum = new float[GW * GH], body = new float[GW * GH];
    readonly Color[] tint = new Color[GW * GH];

    // ---- the room ----------------------------------------------------------------------
    const int FloorW = 64, FloorD = 44;         // floor glyphs, 1 unit apart
    const float FloorZ = 4;                     // the floor's center is a bit behind the dancer
    readonly PVector[] dust = new PVector[260];
    readonly PVector[] ball = new PVector[150];
    static readonly PVector BallAt = new PVector(0, 27, 6);

    // ---- what gets drawn this frame: one entry per glyph ------------------------------
    const int MaxItems = 16000;
    readonly float[] ix = new float[MaxItems], iy = new float[MaxItems], iz = new float[MaxItems];
    readonly int[] icolor = new int[MaxItems];  // packed RGB
    readonly float[] ialpha = new float[MaxItems];
    readonly char[] iglyph = new char[MaxItems];
    readonly float[] depth = new float[MaxItems], sx = new float[MaxItems], sy = new float[MaxItems], ss = new float[MaxItems];
    readonly int[] order = new int[MaxItems];
    int items;
    PFont mono;

    // ---- the camera -----------------------------------------------------------------------
    const float FovY = 60;
    const int SongBeats = 128;                  // 32 bars, then it all loops
    float frozenFor;                            // beats the camera spent frozen
    bool frozen, labels = true;
    double lastBeat;

    // Where the camera is and what it looks at, at a beat of the song (Catmull-Rom through
    // these: smooth, no stops). The dancer stands at x = 0, z = 0, facing -z, chest at y = 9.
    static readonly (float Beat, PVector Pos, PVector Look)[] Path =
    {
        (0,   new PVector(0, 9.5f, 0.2f),    new PVector(0, 9.5f, 10)),    // inside the chest
        (12,  new PVector(0.5f, 9.5f, 0.2f), new PVector(10, 8, 2)),       // look around in there
        (22,  new PVector(0, 7, -1.5f),      new PVector(-4, 14, 6)),      // ...and up
        (32,  new PVector(-5, 4, -9),        new PVector(0, 9, 0)),        // step out, still too close to tell
        (44,  new PVector(6, 4, -20),        new PVector(0, 9, 0)),        // backing away
        (56,  new PVector(0, 8, -34),        new PVector(0, 9.5f, 0)),     // there it is
        (72,  new PVector(-24, 11, -24),     new PVector(0, 9, 0)),        // around to its side: it has depth
        (88,  new PVector(-30, 18, 8),       new PVector(0, 8, 2)),
        (100, new PVector(14, 24, -30),      new PVector(0, 8, 0)),        // up high, the whole room
        (112, new PVector(1, 10.5f, -15),    new PVector(0, 9.5f, 0)),     // and straight back in, through the chest
        (122, new PVector(0, 9.5f, -2.5f),   new PVector(0, 9.5f, 8)),
        (128, new PVector(0, 9.5f, 0.2f),    new PVector(0, 9.5f, 10)),    // = beat 0: the loop closes
    };

    // ---- the song ---------------------------------------------------------------------------
    double musicStart = -1;
    bool heardMusic;
    int padBar, bassBar, leadBar, drumBar, hatBar;

    static readonly string[][] Chords =
    {
        new[] { ""F3"", ""A3"", ""C4"", ""E4"" },   // Fmaj7
        new[] { ""E3"", ""G3"", ""B3"", ""D4"" },   // Em7
        new[] { ""D3"", ""F3"", ""A3"", ""C4"" },   // Dm7
        new[] { ""C3"", ""E3"", ""G3"", ""B3"" },   // Cmaj7
    };
    static readonly string[] Roots = { ""F2"", ""E2"", ""D2"", ""C2"" };

    // Sections, in bars of the 32-bar song.
    static bool Drums(int bar) => bar >= 4 && bar < 28;
    static bool Full(int bar) => bar >= 12 && bar < 28;
    static bool Lead(int bar) => bar >= 16 && bar < 28;

    public override void Setup()
    {
        Size(960, 540);
        Bpm = 116;
        mono = CreateFont(""Consolas"", 32);
        pic = CreateGraphics(PicW, PicH);
        thick = CreateGraphics(PicW, PicH);
        for (int k = 0; k < dust.Length; k++)
            dust[k] = new PVector((Hash(k) - 0.5f) * 60, 1 + Hash(k + 500) * 26, (Hash(k + 900) - 0.5f) * 44 + FloorZ);
        // The mirror ball: points spread evenly over a sphere (a Fibonacci spiral).
        for (int k = 0; k < ball.Length; k++)
        {
            float y = 1 - 2 * (k + 0.5f) / ball.Length, r = Sqrt(1 - y * y), a = k * 2.39996f;
            ball[k] = new PVector(Cos(a) * r, y, Sin(a) * r);
        }
    }

    public override void OnNote(NoteEvent e) => heardMusic = true;

    /// <summary>Beats since the song started (the loops wait for the next bar to start; no sound at all: since Run).</summary>
    double SongBeat()
    {
        double b = Beat();
        if (heardMusic && musicStart < 0)
            musicStart = Math.Floor(b / 4 + 1e-6) * 4;
        if (musicStart < 0)
            return Millis() > 2500 ? b : 0;
        return Math.Max(0, b - musicStart);
    }

    public override void Draw()
    {
        double song = SongBeat();
        if (frozen) frozenFor += (float)(song - lastBeat);
        lastBeat = song;
        float beat = (float)song;
        int bar = (int)(beat / 4) % 32;
        float kick = Drums(bar) ? Pulse(1, 5) : 0;

        items = 0;
        PoseDancer(beat);
        AddStatue(kick);
        AddFloor(beat, bar, kick);
        AddBeams(beat, kick);
        AddBall(beat, kick);
        AddDust(beat);

        Background(6, 4, 14);
        Render((float)(song - frozenFor));
        if (labels) DrawLabels(song);
    }

    // =========================================================================================
    // The dancer: drawn flat, twice (its colors, and how thick each part is), then stood up.
    // =========================================================================================
    void PoseDancer(float beat)
    {
        float step = Sin(beat * PI);                    // a side step every beat
        float bounce = Abs(Sin(beat * PI)) * 3;
        float hipX = step * 5, hipY = -26 + bounce;
        float neckX = hipX - step * 2, neckY = hipY - 26;
        int move = (int)(beat / 8) % 4;
        float ph = beat * PI;

        // Arms: (shoulder angle, elbow angle) per side; radians, 0 = pointing right, up = -PI/2.
        (float s, float e) left, right;
        switch (move)
        {
            case 0:  // pump: fists go up one at a time
                left = (-PI * 0.75f - Max(0, Sin(ph)) * 0.9f, -PI * 0.5f);
                right = (-PI * 0.25f - Max(0, -Sin(ph)) * 0.9f, -PI * 0.5f);
                break;
            case 1:  // point: one arm up to the corner, then down across
                bool up = Sin(ph / 2) > 0;
                right = up ? (-PI * 0.3f, -PI * 0.35f) : (PI * 0.25f, PI * 0.35f);
                left = (PI * 0.7f, PI * 0.6f);
                break;
            case 2:  // wave: both arms up, swaying
                left = (-PI * 0.62f + Sin(ph) * 0.25f, -PI * 0.55f + Sin(ph) * 0.3f);
                right = (-PI * 0.38f + Sin(ph) * 0.25f, -PI * 0.45f + Sin(ph) * 0.3f);
                break;
            default: // roll: forearms circling in front of the chest
                left = (PI * 0.85f, -PI * 0.1f + Sin(ph * 2) * 0.8f);
                right = (PI * 0.15f, PI * 1.1f + Cos(ph * 2) * 0.8f);
                break;
        }

        // Same figure on both canvases: `pic` in color, `thick` in grays (white = thickest).
        foreach (var g in new[] { pic, thick })
        {
            bool colors = g == pic;
            g.BeginDraw();
            g.Background(0);
            g.PushMatrix();
            g.Translate(PicW / 2f, PicH - 2);
            g.Scale(1.4f);
            g.StrokeCap(StrokeCapKind.Round);
            // Legs: knees bend in turn.
            if (colors) g.Stroke(90, 200, 255); else g.Stroke(150);
            g.StrokeWeight(5);
            for (int side = -1; side <= 1; side += 2)
            {
                float bend = Max(0, side * step) * 6;
                float kneeX = hipX + side * 6 + side * bend * 0.4f, kneeY = hipY + 12 - bend * 0.3f;
                g.Line(hipX + side * 3, hipY, kneeX, kneeY);
                g.Line(kneeX, kneeY, hipX + side * 9, -1);
            }
            // Body.
            if (colors) g.Stroke(255, 120, 190); else g.Stroke(255);
            g.StrokeWeight(9);
            g.Line(hipX, hipY - 2, neckX, neckY + 4);
            // Arms.
            if (colors) g.Stroke(255, 200, 150); else g.Stroke(110);
            g.StrokeWeight(4);
            Arm(g, neckX - 4, neckY + 3, left);
            Arm(g, neckX + 4, neckY + 3, right);
            // Head, with shades.
            g.NoStroke();
            if (colors) g.Fill(255, 215, 170); else g.Fill(220);
            g.Circle(neckX, neckY - 6, 13);
            if (colors) { g.Fill(40, 230, 255); g.Rect(neckX - 6, neckY - 8, 12, 3); }
            g.PopMatrix();
            g.LoadPixels();
            var px = g.Pixels;
            for (int r = 0; r < GH; r++)
                for (int c = 0; c < GW; c++)
                {
                    int R = 0, G = 0, B = 0;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            var p = px[(r * 2 + dy) * PicW + c * 2 + dx];
                            R += p.R; G += p.G; B += p.B;
                        }
                    int i = r * GW + c;
                    if (colors)
                    {
                        lum[i] = (0.3f * R + 0.55f * G + 0.15f * B) / (4 * 255f);
                        tint[i] = Color(R / 4, G / 4, B / 4);
                    }
                    else
                        body[i] = R / (4 * 255f);
                }
            g.EndDraw();
        }
        // Blurred, the thickness is greatest along the middle of each limb and fades
        // towards its edges: so the statue comes out rounded, not cut from a flat sheet.
        // (A small box blur by hand, on the 96 x 54 grid: much cheaper than Filter(Blur).)
        BoxBlur(body, GW, GH);
        BoxBlur(body, GW, GH);
    }

    readonly float[] blurTmp = new float[GW * GH];

    void BoxBlur(float[] a, int w, int h)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0; int n = 0;
                for (int k = -1; k <= 1; k++)
                    if (x + k >= 0 && x + k < w) { s += a[y * w + x + k]; n++; }
                blurTmp[y * w + x] = s / n;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0; int n = 0;
                for (int k = -1; k <= 1; k++)
                    if (y + k >= 0 && y + k < h) { s += blurTmp[(y + k) * w + x]; n++; }
                a[y * w + x] = s / n;
            }
    }

    static void Arm(PGraphics g, float x, float y, (float s, float e) a)
    {
        const float upper = 11, fore = 10;
        float ex = x + MathF.Cos(a.s) * upper, ey = y + MathF.Sin(a.s) * upper;
        g.Line(x, y, ex, ey);
        g.Line(ex, ey, ex + MathF.Cos(a.e) * fore, ey + MathF.Sin(a.e) * fore);
    }

    /// <summary>Every cell the dancer covers becomes a short row of glyphs in Z: 1 to 5 deep.</summary>
    void AddStatue(float kick)
    {
        for (int r = 0; r < GH; r++)
            for (int c = 0; c < GW; c++)
            {
                int i = r * GW + c;
                float v = lum[i];
                if (v < 0.08f) continue;
                float x = (GW / 2f - c - 0.5f) * Cell;                  // mirrored: it faces the camera at -z
                float y = (GH - r - 0.5f) * Cell;
                int layers = 1 + (int)MathF.Round(body[i] * 9);         // up to 10 deep in the torso, 2-4 in the arms
                var col = tint[i];
                float glow = 1.15f + 0.4f * kick;
                int rgb = Pack(col.R * glow + 20, col.G * glow + 15, col.B * glow + 25);
                int g = (int)Constrain(Sqrt(v) * Glyphs.Length, 2, Glyphs.Length - 1);
                for (int k = 0; k < layers; k++)
                {
                    float z = (k - (layers - 1) / 2f) * Cell * 1.1f;
                    bool face = k == 0 || k == layers - 1;
                    // The surfaces get the dense glyphs, the inside lighter ones.
                    Add(x, y, z, face ? Glyphs[g] : Glyphs[Math.Max(2, g - 3)], rgb, face ? 1 : 0.55f);
                }
            }
    }

    /// <summary>The dance floor: tiles of glyphs; lit tiles rise with the kick, a ripple runs from the feet.</summary>
    void AddFloor(float beat, int bar, float kick)
    {
        for (int fz = 0; fz < FloorD; fz++)
            for (int fx = 0; fx < FloorW; fx++)
            {
                float x = fx - FloorW / 2f + 0.5f, z = fz - FloorD / 2f + 0.5f + FloorZ;
                int tx = (int)Math.Floor(x / 5), tz = (int)Math.Floor(z / 5);
                bool lit = Drums(bar) && (((tx + tz + (int)beat) % 3 + 3) % 3 == 0);
                bool edge = ((fx + 1000) % 5 == 0) || ((fz + 1000) % 5 == 0);
                float dist = Sqrt(x * x + z * z);
                float ripple = Max(0, Sin(dist * 0.45f - beat * PI)) * (0.15f + 0.5f * kick) * Exp(-dist * 0.04f);
                if (lit)
                {
                    float hue = ((bar * 47 + tx * 20 + tz * 60) % 360 + 360) % 360;
                    var c = HsbRgb(hue, 75, 100);
                    float h = 0.2f + 1.4f * kick + ripple;
                    Add(x, h, z, edge ? '=' : '#', c, 0.95f);
                    if (h > 0.8f) Add(x, h - 0.7f, z, '+', c, 0.5f);           // lit tiles are blocks, not decals
                }
                else
                    Add(x, ripple, z, edge ? '-' : '.', Pack(70, 55, 120), edge ? 0.6f : 0.35f);
            }
    }

    /// <summary>Two spotlights: dotted beams from the ceiling, sweeping across the floor.</summary>
    void AddBeams(float beat, float kick)
    {
        for (int s = 0; s < 2; s++)
        {
            var from = new PVector(s == 0 ? -22 : 22, 30, 14);
            float a = beat * PI / 8 + s * PI;
            var to = new PVector(Sin(a) * 14, 0, Cos(a * 0.7f) * 9 + 2);
            int rgb = s == 0 ? Pack(255, 110, 210) : Pack(120, 210, 255);
            for (int k = 0; k < 46; k++)
            {
                float t = k / 45f;
                // A cone: the beam widens towards the floor.
                float spread = t * 2.2f, ang = k * 2.39996f;
                Add(Lerp(from.X, to.X, t) + Cos(ang) * spread, Lerp(from.Y, to.Y, t), Lerp(from.Z, to.Z, t) + Sin(ang) * spread,
                    k % 3 == 0 ? ':' : '.', rgb, 0.25f + 0.35f * kick);
            }
        }
    }

    /// <summary>The mirror ball: a sphere of glyphs turning, its facets catching the light.</summary>
    void AddBall(float beat, float kick)
    {
        float spin = beat * 0.4f, cs = Cos(spin), sn = Sin(spin);
        for (int k = 0; k < ball.Length; k++)
        {
            var p = ball[k];
            float x = p.X * cs - p.Z * sn, z = p.X * sn + p.Z * cs;
            bool flash = Hash(k + (int)(beat * 4) * 131) > 0.9f;
            Add(BallAt.X + x * 2.4f, BallAt.Y + p.Y * 2.4f, BallAt.Z + z * 2.4f, flash ? '@' : '#',
                flash ? Pack(255, 255, 255) : Pack(170, 175, 200), flash ? 1 : 0.7f);
        }
    }

    /// <summary>Dust in the air, at every height, drifting round the dancer.</summary>
    void AddDust(float beat)
    {
        float turn = beat * 0.03f;
        for (int k = 0; k < dust.Length; k++)
        {
            var p = dust[k];
            float a = turn * (0.5f + Hash(k + 77));
            float x = p.X * Cos(a) - (p.Z - FloorZ) * Sin(a), z = p.X * Sin(a) + (p.Z - FloorZ) * Cos(a) + FloorZ;
            float y = p.Y + Sin(beat * 0.5f + k) * 0.6f;
            Add(x, y, z, k % 7 == 0 ? '*' : k % 3 == 0 ? '+' : '.', HsbRgb((k * 37) % 360, 35, 100), 0.55f);
        }
    }

    void Add(float x, float y, float z, char glyph, int rgb, float alpha)
    {
        if (items >= MaxItems) return;
        ix[items] = x; iy[items] = y; iz[items] = z;
        iglyph[items] = glyph; icolor[items] = rgb; ialpha[items] = alpha;
        items++;
    }

    static int Pack(float r, float g, float b) =>
        ((int)Math.Clamp(r, 0, 255) << 16) | ((int)Math.Clamp(g, 0, 255) << 8) | (int)Math.Clamp(b, 0, 255);

    static int HsbRgb(float h, float s, float v)
    {
        s /= 100; v /= 100;
        float c = v * s, hp = h / 60 % 6, x = c * (1 - MathF.Abs(hp % 2 - 1)), m = v - c;
        (float r, float g, float b) = hp < 1 ? (c, x, 0f) : hp < 2 ? (x, c, 0f) : hp < 3 ? (0f, c, x) : hp < 4 ? (0f, x, c) : hp < 5 ? (x, 0f, c) : (c, 0f, x);
        return Pack((r + m) * 255, (g + m) * 255, (b + m) * 255);
    }

    static float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n;
            x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
            return (x & 0xFFFFFF) / 16777215f;
        }
    }

    // =========================================================================================
    // The camera: project every glyph, sort far to near, draw.
    // =========================================================================================
    void Render(float camBeat)
    {
        var (eye, look) = CameraAt(camBeat);
        var f = PVector.Sub(look, eye).Normalize();
        var r = f.Cross(new PVector(0, 1, 0)).Normalize();
        var u = r.Cross(f);
        float focal = Height / 2f / Tan(Radians(FovY) / 2);

        int n = 0;
        for (int k = 0; k < items; k++)
        {
            float dx = ix[k] - eye.X, dy = iy[k] - eye.Y, dz = iz[k] - eye.Z;
            float zc = dx * f.X + dy * f.Y + dz * f.Z;
            if (zc < 0.3f) continue;                                  // behind us, or in our face
            float xc = dx * r.X + dy * r.Y + dz * r.Z;
            float yc = dx * u.X + dy * u.Y + dz * u.Z;
            float size = focal * 0.6f / zc;
            float x = Width / 2f + focal * xc / zc, y = Height / 2f - focal * yc / zc;
            if (size < 2.5f || x < -size || x > Width + size || y < -size || y > Height + size) continue;
            sx[k] = x; sy[k] = y; ss[k] = Min(size, 120);
            depth[n] = -zc;                                           // far first
            order[n] = k;
            n++;
        }
        System.Array.Sort(depth, order, 0, n);

        TextFont(mono, 32);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        for (int q = 0; q < n; q++)
        {
            int k = order[q];
            float z = -depth[q];
            // Close glyphs fade in (no wall of characters on the lens); far ones fade into the dark.
            float alpha = 255 * ialpha[k] * Constrain((z - 0.3f) / 2f, 0, 1) * Constrain(1.25f - z / 90, 0.35f, 1);
            int c = icolor[k];
            Fill((c >> 16) & 255, (c >> 8) & 255, c & 255, alpha);
            // Whole-pixel sizes (steps of 6 when big): Skia caches each glyph per
            // size, so a few sizes draw ~5x faster than thousands of slightly different ones.
            TextSize(ss[k] < 24 ? MathF.Round(ss[k]) : MathF.Round(ss[k] / 6) * 6);
            Text(iglyph[k].ToString(), sx[k], sy[k]);
        }
    }

    (PVector Eye, PVector Look) CameraAt(float beat)
    {
        float b = ((beat % SongBeats) + SongBeats) % SongBeats;
        int k = 0;
        while (k < Path.Length - 2 && Path[k + 1].Beat <= b) k++;
        float t = (b - Path[k].Beat) / (Path[k + 1].Beat - Path[k].Beat);
        int k0 = Math.Max(0, k - 1), k3 = Math.Min(Path.Length - 1, k + 2);
        var eye = CatmullRom(Path[k0].Pos, Path[k].Pos, Path[k + 1].Pos, Path[k3].Pos, t);
        var look = CatmullRom(Path[k0].Look, Path[k].Look, Path[k + 1].Look, Path[k3].Look, t);
        return (eye, look);
    }

    static PVector CatmullRom(PVector p0, PVector p1, PVector p2, PVector p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        float F(float a, float b, float c, float d) =>
            0.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
        return new PVector(F(p0.X, p1.X, p2.X, p3.X), F(p0.Y, p1.Y, p2.Y, p3.Y), F(p0.Z, p1.Z, p2.Z, p3.Z));
    }

    void DrawLabels(double song)
    {
        int bar = (int)(song / 4) % 32;
        string part = bar < 4 ? ""intro"" : bar < 12 ? ""groove"" : bar < 16 ? ""build"" : bar < 28 ? ""chorus"" : ""breakdown"";
        TextFont(mono, 13);
        TextSize(13);
        TextAlign(TextAlignH.Left, TextAlignV.Bottom);
        Fill(255, 255, 255, 110);
        Text($""bar {bar + 1}/32 · {part} · {items:N0} glyphs in 3D{(frozen ? "" · camera frozen"" : """")}"", 16, Height - 30);
        Fill(255, 255, 255, 60);
        Text(""Space: freeze camera · H: hide labels"", 16, Height - 12);
    }

    public override void KeyPressed()
    {
        if (Key == ' ') frozen = !frozen;
        else if (Key == 'h' || Key == 'H') labels = !labels;
    }

    // ===========================================================================
    // The song: 116 BPM, 32 bars, looping. Fmaj7 - Em7 - Dm7 - Cmaj7.
    // ===========================================================================
    [LiveLoop]
    async Task Pad()
    {
        int bar = padBar++ % 32;
        foreach (var note in Chords[bar % 4])
            Play(note, amp: Full(bar) ? 0.06 : 0.09, attack: 0.4, sustain: 2.6, release: 1.0,
                 cutoff: Full(bar) ? 90 : 80, pan: 0, synth: Synth.Saw);
        await Sleep(4);
    }

    [LiveLoop]
    async Task Bass()
    {
        int bar = bassBar++ % 32;
        double root = Notes.Parse(Roots[bar % 4]);
        for (int i = 0; i < 8; i++)
        {
            // Disco octaves: low, high, low, high...
            if (Drums(bar))
                Play(root + (i % 2 == 1 ? 12 : 0), amp: 0.26, release: 0.22, cutoff: 76, res: 0.3, synth: Synth.Saw);
            else if (i == 0)
                Play(root, amp: 0.18, release: 1.6, cutoff: 64, synth: Synth.Triangle);
            await Sleep(0.5);
        }
    }

    [LiveLoop]
    async Task Lead()
    {
        int bar = leadBar++ % 32;
        // A two-bar hook: the first bar climbs, the second one comes home.
        (string Note, double Beats)[] climb = { (""E5"", 0.5), (""G5"", 0.5), (""A5"", 1), (""G5"", 0.5), (""E5"", 0.5), (""D5"", 1) };
        (string Note, double Beats)[] home = { (""C5"", 0.5), (""D5"", 0.5), (""E5"", 1.5), ("""", 0.5), (""G4"", 0.5), (""A4"", 0.5) };
        var phrase = bar % 2 == 0 ? climb : home;
        // Every fourth bar answers an octave lower, for variety.
        int shift = bar % 8 == 7 ? -12 : 0;
        foreach (var (note, beats) in phrase)
        {
            if (Lead(bar) && note != """")
            {
                double n = Notes.Parse(note) + shift;
                Play(n, amp: 0.10, attack: 0.01, release: beats * 0.45, cutoff: 96, pan: -0.15, synth: Synth.Square);
                Play(n + 12, amp: 0.035, release: beats * 0.6, pan: 0.25, synth: Synth.Triangle);   // a soft octave on top
            }
            await Sleep(beats);
        }
    }

    [LiveLoop]
    async Task Kit()
    {
        int bar = drumBar++ % 32;
        for (int beat = 0; beat < 4; beat++)
        {
            if (Drums(bar))
                Sample(""kick"", amp: 0.9);
            if (Full(bar) && beat % 2 == 1)
                Sample(""snare"", amp: 0.55);
            // The build: snare rolls getting faster in the last bar before the chorus.
            if (bar == 15)
            {
                int hits = beat < 2 ? 2 : 4;
                for (int h = 0; h < hits; h++)
                {
                    Sample(""snare"", amp: 0.25 + 0.1 * beat);
                    await Sleep(1.0 / hits);
                }
                continue;
            }
            await Sleep(1);
        }
    }

    [LiveLoop]
    async Task Hats()
    {
        int bar = hatBar++ % 32;
        for (int i = 0; i < 8; i++)
        {
            if (Drums(bar))
                Sample(""hat"", amp: i % 2 == 1 ? (Full(bar) ? 0.32 : 0.22) : 0.1, pan: 0.25);
            await Sleep(0.5);
        }
    }
}
";

        private const string AsciiMind =
@"// A neural network learns to write a word, live, and you watch it think in
// ASCII.
//
// No download and no library: the whole network is the Brain class at the
// bottom, plain C#. It only knows one thing: given a point (x, y) on the
// screen, how much ink goes there? It starts with random weights, so the
// screen is noise. Every frame it looks at a few hundred random points of the
// word, sees how wrong it was, and nudges its ~2,300 weights a little
// (backpropagation + Adam). In a few seconds the letters come out of the noise.
//
// Each character on screen is one question to the network: the more ink it
// answers, the denser the glyph. Where its answer is still changing -- where
// it's learning right now -- the glyph glows.
//
// - Type: a new word. The network doesn't start over: it unlearns the old
//   word while it learns the new one, so one melts into the other.
// - Enter: forget everything (new random weights) and learn from zero.
// - Mouse: swirls the network's input around the pointer. The letters bend,
//   because the network doesn't store a picture -- it's a function of (x, y).
// - Wheel: zoom out to see what it imagines outside the word.
// - Tab: other glyphs.  Space: pause the learning.
public class AsciiMind : Sketch
{
    const int Cols = 96, Rows = 30;                  // the ASCII screen
    const float CellW = 10, CellH = 18;
    const int SubX = 3, SubY = 5;                    // the word is learned a bit finer than it's shown
    const int StepsPerFrame = 4, BatchSize = 128;    // how much it practices per frame

    readonly string[] glyphSets = { "" .:-=+*#%@"", "" .oO0@"", "" 01"", "" .,;!|I#"", "" ·•●"" };
    int glyphSet;

    Brain brain;
    readonly System.Random rng = new System.Random(7);
    float[] target;                                  // the word as ink, 0..1, tw x th
    int tw = Cols * SubX, th = Rows * SubY;
    string word = ""HOLA"";
    PFont mono, heavy;

    readonly float[] ink = new float[Cols * Rows];   // the network's last answer for each character
    readonly float[] glow = new float[Cols * Rows];  // how much that answer moved lately
    float zoom = 1, shownZoom = 1;
    bool paused;
    readonly float[] lossHistory = new float[120];
    int lossCount;

    // For the HUD (and the tests).
    public float Loss { get; private set; } = 0.25f;
    public int Steps { get; private set; }
    public string Word => word;
    public float InkAt(int col, int row) => ink[row * Cols + col];

    public override void Setup()
    {
        Size(960, 640);
        mono = CreateFont(""Consolas"", 16);
        heavy = CreateFont(""Arial Black"", 100);
        brain = new Brain(rng);
        Learn(word);
    }

    /// <summary>Paints the word offscreen and keeps its pixels: that's what the network has to match.</summary>
    public void Learn(string newWord)
    {
        word = newWord.ToUpperInvariant();
        string text = word.Length == 0 ? ""?"" : word;
        var pg = CreateGraphics(tw, th);
        pg.BeginDraw();
        pg.Background(0);
        pg.NoStroke();
        pg.Fill(255);
        pg.TextFont(heavy);
        pg.TextAlign(TextAlignH.Center, TextAlignV.Center);
        // This canvas is squashed like the screen (a character is SubX x SubY
        // pixels here, CellW x CellH there), so draw the letters stretched to match.
        float stretch = (SubY / (float)SubX) / (CellH / CellW);
        pg.TextSize(100);
        float size = Min(100 * tw * 0.86f / Max(1, pg.TextWidth(text)), th * 0.7f / stretch);
        pg.TextSize(size);
        pg.PushMatrix();
        pg.Translate(tw / 2f, th / 2f);
        pg.Scale(1, stretch);
        pg.Text(text, 0, 0);
        pg.PopMatrix();
        pg.LoadPixels();
        target = new float[tw * th];
        for (int i = 0; i < target.Length; i++)
            target[i] = pg.Pixels[i].R / 255f;
        pg.EndDraw();
        pg.Dispose();
        // Soft edges: a light blur, so the borders of the letters come out as . : - =
        Blur(target, tw, th, 2);
        Blur(target, tw, th, 2);
    }

    public override void Draw()
    {
        Background(8, 9, 20);
        if (!paused)
            for (int s = 0; s < StepsPerFrame; s++)
                Train();

        // Ask the network about half the characters each frame (alternate rows):
        // the screen still updates 30 times a second and it costs half.
        shownZoom = Lerp(shownZoom, zoom, 0.12f);
        bool bend = MouseY > 0 && MouseY < Rows * CellH;
        float t = Millis() / 1000f;
        float left = (Width - Cols * CellW) / 2;
        for (int r = FrameCount % 2; r < Rows; r += 2)
            for (int c = 0; c < Cols; c++)
            {
                float x = (c + 0.5f) / Cols, y = (r + 0.5f) / Rows;
                if (bend)
                {
                    float dx = left + (c + 0.5f) * CellW - MouseX, dy = (r + 0.5f) * CellH - MouseY;
                    float a = 1.6f * Exp(-(dx * dx + dy * dy) / (2 * 110f * 110f)) * Sin(t * 1.7f);
                    x += (dx * Cos(a) - dy * Sin(a) - dx) / (Cols * CellW);
                    y += (dx * Sin(a) + dy * Cos(a) - dy) / (Rows * CellH);
                }
                float answer = brain.Think(0.5f + (x - 0.5f) * shownZoom, 0.5f + (y - 0.5f) * shownZoom);
                int i = r * Cols + c;
                glow[i] = Max(glow[i] * 0.9f, Abs(answer - ink[i]) * 12);
                ink[i] = answer;
            }

        TextFont(mono, 16);
        TextAlign(TextAlignH.Center, TextAlignV.Center);
        string glyphs = glyphSets[glyphSet];
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                int i = r * Cols + c;
                float v = ink[i], hot = Min(glow[i], 1);
                int g = (int)Constrain(v * glyphs.Length, 0, glyphs.Length - 1);
                if (g == 0 && hot < 0.25f)
                    continue;
                if (g == 0) g = 1;
                // Violet to pink across the screen; where it's learning, it warms up
                // through red to amber (400 = 40 degrees, one turn later).
                float hue = 245 + c * 0.8f + 12 * Sin(t * 0.5f + r * 0.15f);
                FillHSB(Lerp(hue, 400, hot * 0.85f) % 360, 65 - 20 * hot, 28 + 72 * Max(v, hot));
                Text(glyphs[g].ToString(), left + (c + 0.5f) * CellW, (r + 0.5f) * CellH);
            }

        DrawHud();
    }

    /// <summary>One lesson: a batch of random pixels of the word -- guess, compare, nudge every weight.</summary>
    void Train()
    {
        brain.BeginBatch();
        float sum = 0;
        for (int b = 0; b < BatchSize; b++)
        {
            int px = rng.Next(tw), py = rng.Next(th);
            sum += brain.Practice((px + 0.5f) / tw, (py + 0.5f) / th, target[py * tw + px]);
        }
        brain.EndBatch(BatchSize);
        Steps++;
        Loss = Lerp(Loss, sum / BatchSize, 0.05f);
        if (Steps % 8 == 0)
            lossHistory[lossCount++ % lossHistory.Length] = Loss;
    }

    void DrawHud()
    {
        float top = Rows * CellH + 20;
        NoStroke();
        Fill(255, 255, 255, 10);
        Rect(0, top - 10, Width, Height - top + 10);

        TextFont(mono, 15);
        TextAlign(TextAlignH.Left, TextAlignV.Top);
        Fill(235, 225, 255);
        Text(word + (FrameCount / 30 % 2 == 0 ? ""_"" : """"), 28, top + 4);
        Fill(140, 145, 180);
        Text($""network {Brain.Inputs} → {Brain.Hidden} → {Brain.Hidden} → 1 · {brain.WeightCount:N0} weights · lesson {Steps:N0} · error {Loss:0.0000}{(paused ? "" · paused"" : """")}"", 28, top + 28);
        Fill(95, 100, 135);
        Text(""type: new word · Enter: forget · mouse: bend · wheel: zoom · Tab: glyphs · Space: pause"", 28, top + 52);

        // The error, lesson after lesson: it falls as the network learns.
        float gx = Width - 210, gy = top + 4, gw = 180, gh = 62;
        NoFill();
        Stroke(255, 255, 255, 30);
        StrokeWeight(1);
        Rect(gx, gy, gw, gh);
        Stroke(255, 170, 110);
        StrokeWeight(1.5f);
        BeginShape();
        int n = Min(lossCount, lossHistory.Length);
        for (int k = 0; k < n; k++)
        {
            float v = lossHistory[(lossCount - n + k) % lossHistory.Length];
            Vertex(gx + gw * k / (lossHistory.Length - 1f), gy + gh - Constrain(Sqrt(v / 0.12f), 0, 1) * (gh - 4) - 2);
        }
        EndShape();
        NoStroke();
    }

    public override void KeyTyped()
    {
        if (Key == BACKSPACE)
        {
            if (word.Length > 0) Learn(word[..^1]);
        }
        else if (Key == ENTER || Key == RETURN)
        {
            brain = new Brain(rng);
            Steps = 0;
            lossCount = 0;
        }
        else if (Key == TAB)
            glyphSet = (glyphSet + 1) % glyphSets.Length;
        else if (Key == ' ')
            paused = !paused;
        else if (!char.IsControl(Key) && word.Length < 10)
            Learn(word + Key);
    }

    public override void MouseWheel(float delta) => zoom = Constrain(zoom * (delta > 0 ? 1.2f : 1 / 1.2f), 0.6f, 8);

    static void Blur(float[] img, int w, int h, int radius)
    {
        var tmp = new float[img.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0; int n = 0;
                for (int k = -radius; k <= radius; k++)
                    if (x + k >= 0 && x + k < w) { s += img[y * w + x + k]; n++; }
                tmp[y * w + x] = s / n;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0; int n = 0;
                for (int k = -radius; k <= radius; k++)
                    if (y + k >= 0 && y + k < h) { s += tmp[(y + k) * w + x]; n++; }
                img[y * w + x] = s / n;
            }
    }

    /// <summary>
    /// The whole ""AI"": a small multilayer perceptron, (x, y) -> ink.
    /// The point first becomes 32 sine waves of random directions and sizes
    /// (""Fourier features"": a plain network is bad at sharp edges, waves give
    /// it something to build them with). Then two layers of 24 neurons (tanh)
    /// and one output (sigmoid, 0..1). Learns by backpropagation, with Adam.
    /// </summary>
    public class Brain
    {
        public const int Waves = 32, Inputs = Waves * 2, Hidden = 24;
        readonly float[] waveX = new float[Waves], waveY = new float[Waves];
        readonly float[] w1 = new float[Inputs * Hidden], b1 = new float[Hidden];   // w1[j * Inputs + i]: input i -> neuron j
        readonly float[] w2 = new float[Hidden * Hidden], b2 = new float[Hidden];
        readonly float[] w3 = new float[Hidden];
        float b3;
        readonly float[] input = new float[Inputs], h1 = new float[Hidden], h2 = new float[Hidden], d2 = new float[Hidden];
        // Gradients and Adam's memory, for all the weights in one row: w1 b1 w2 b2 w3 b3.
        readonly float[] grad, m, v;
        int lessons;

        public int WeightCount => grad.Length;

        public Brain(System.Random rng)
        {
            float Gauss() => (float)(System.Math.Sqrt(-2 * System.Math.Log(1 - rng.NextDouble())) * System.Math.Cos(2 * System.Math.PI * rng.NextDouble()));
            // Mostly gentle waves, some fast ones (letters are wide but their edges are sharp).
            // y is squeezed: on screen 1.0 of height is 0.56 of the width.
            for (int k = 0; k < Waves; k++) { waveX[k] = Gauss() * 10; waveY[k] = Gauss() * 10 * 0.5625f; }
            for (int k = 0; k < w1.Length; k++) w1[k] = Gauss() / MathF.Sqrt(Inputs);
            for (int k = 0; k < w2.Length; k++) w2[k] = Gauss() / MathF.Sqrt(Hidden);
            for (int k = 0; k < w3.Length; k++) w3[k] = Gauss() / MathF.Sqrt(Hidden);
            int n = w1.Length + Hidden + w2.Length + Hidden + Hidden + 1;
            grad = new float[n]; m = new float[n]; v = new float[n];
        }

        float Forward(float x, float y)
        {
            x = x * 2 - 1; y = y * 2 - 1;
            for (int k = 0; k < Waves; k++)
            {
                float a = waveX[k] * x + waveY[k] * y;
                input[2 * k] = MathF.Sin(a);
                input[2 * k + 1] = MathF.Cos(a);
            }
            for (int j = 0; j < Hidden; j++)
            {
                float s = b1[j];
                int row = j * Inputs;
                for (int i = 0; i < Inputs; i++) s += w1[row + i] * input[i];
                h1[j] = MathF.Tanh(s);
            }
            for (int j = 0; j < Hidden; j++)
            {
                float s = b2[j];
                int row = j * Hidden;
                for (int i = 0; i < Hidden; i++) s += w2[row + i] * h1[i];
                h2[j] = MathF.Tanh(s);
            }
            float o = b3;
            for (int i = 0; i < Hidden; i++) o += w3[i] * h2[i];
            return 1 / (1 + MathF.Exp(-o));
        }

        /// <summary>How much ink the network thinks is at (x, y), both 0..1 across the screen.</summary>
        public float Think(float x, float y) => Forward(x, y);

        public void BeginBatch() => System.Array.Clear(grad);

        /// <summary>Guesses one point, adds to every weight's gradient how it should move, returns the squared error.</summary>
        public float Practice(float x, float y, float want)
        {
            float got = Forward(x, y);
            float err = got - want;
            float dOut = err * got * (1 - got);          // through the sigmoid
            int gw1 = 0, gb1 = w1.Length, gw2 = gb1 + Hidden, gb2 = gw2 + w2.Length, gw3 = gb2 + Hidden, gb3 = gw3 + Hidden;
            grad[gb3] += dOut;
            for (int j = 0; j < Hidden; j++)
            {
                grad[gw3 + j] += dOut * h2[j];
                d2[j] = dOut * w3[j] * (1 - h2[j] * h2[j]);
            }
            for (int j = 0; j < Hidden; j++)
            {
                float d = d2[j];
                grad[gb2 + j] += d;
                int row = j * Hidden;
                for (int i = 0; i < Hidden; i++) grad[gw2 + row + i] += d * h1[i];
            }
            for (int i = 0; i < Hidden; i++)
            {
                float s = 0;
                for (int j = 0; j < Hidden; j++) s += d2[j] * w2[j * Hidden + i];
                float d = s * (1 - h1[i] * h1[i]);
                grad[gb1 + i] += d;
                int row = i * Inputs;
                for (int k = 0; k < Inputs; k++) grad[gw1 + row + k] += d * input[k];
            }
            return err * err;
        }

        /// <summary>Adam: every weight steps against its average gradient, scaled by how noisy that gradient has been.</summary>
        public void EndBatch(int batchSize, float rate = 0.003f)
        {
            lessons++;
            float c1 = 1 - MathF.Pow(0.9f, lessons), c2 = 1 - MathF.Pow(0.999f, lessons);
            int at = 0;
            Step(w1); Step(b1); Step(w2); Step(b2); Step(w3);
            float[] last = { b3 };
            Step(last);
            b3 = last[0];

            void Step(float[] weights)
            {
                for (int i = 0; i < weights.Length; i++, at++)
                {
                    float g = grad[at] / batchSize;
                    m[at] = 0.9f * m[at] + 0.1f * g;
                    v[at] = 0.999f * v[at] + 0.001f * g * g;
                    weights[i] -= rate * (m[at] / c1) / (MathF.Sqrt(v[at] / c2) + 1e-7f);
                }
            }
        }
    }
}
";

        private const string SerialArduino =
@"// nuget: System.IO.Ports, 10.0.12

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;

// Arduino -> sketch and sketch -> Arduino over USB serial: Processing's
// classic Serial library, here as one `// nuget:` line. The Arduino sends one
// number per line (a potentiometer, a light sensor...) and the sketch plots it;
// hold the mouse down and the sketch lights the Arduino's LED.
//
// The Arduino side -- upload this first:
//
//   void setup() { Serial.begin(115200); pinMode(LED_BUILTIN, OUTPUT); }
//   void loop() {
//     Serial.println(analogRead(A0));
//     if (Serial.available()) digitalWrite(LED_BUILTIN, Serial.read() == '1' ? HIGH : LOW);
//     delay(10);
//   }
//
// No Arduino plugged in? The sketch fakes a sensor with Noise() so you still
// see it work. OnStop() closes the port, so the next Run can open it again
// (otherwise the port stays ""in use"" until the IDE closes).
public class MySketch : Sketch
{
    private const int Baud = 115200;
    private const int Samples = 400;

    private SerialPort? _port;
    private string _status = """";
    private readonly object _lock = new object();
    private readonly Queue<float> _values = new Queue<float>(); // 0..1, newest last
    private string _pending = """";

    public override void Setup()
    {
        Size(820, 480);

        string[] names = SerialPort.GetPortNames();
        if (names.Length == 0)
        {
            _status = ""No serial port found -- simulating a sensor"";
            return;
        }

        string name = names[names.Length - 1]; // usually the one plugged in last
        try
        {
            var port = new SerialPort(name, Baud) { DtrEnable = true };
            port.DataReceived += (s, e) => OnData(port);
            port.Open();
            _port = port;
            _status = $""{name} @ {Baud} baud"";
        }
        catch (Exception ex)
        {
            _status = $""Couldn't open {name}: {ex.Message} -- simulating"";
        }
    }

    // Runs on the serial port's own thread: split what arrived into lines.
    private void OnData(SerialPort port)
    {
        string chunk;
        try { chunk = port.ReadExisting(); }
        catch { return; }

        lock (_lock)
        {
            _pending += chunk;
            int newline;
            while ((newline = _pending.IndexOf('\n')) >= 0)
            {
                string line = _pending.Substring(0, newline).Trim();
                _pending = _pending.Substring(newline + 1);
                if (float.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out float raw))
                    Push(raw / 1023f);
            }
        }
    }

    private void Push(float value)
    {
        _values.Enqueue(Constrain(value, 0, 1));
        while (_values.Count > Samples)
            _values.Dequeue();
    }

    public override void Draw()
    {
        Background(16, 18, 26);

        float[] values;
        lock (_lock)
        {
            if (_port == null)
                Push(Noise(FrameCount * 0.02f));
            values = _values.ToArray();
        }

        // The trace, oldest on the left.
        NoFill();
        Stroke(110, 220, 160);
        StrokeWeight(2);
        BeginShape();
        for (int i = 0; i < values.Length; i++)
            Vertex(Map(i, 0, Samples - 1, 40, Width - 240), Map(values[i], 0, 1, Height - 70, 50));
        EndShape();

        // The latest value, big.
        float latest = values.Length > 0 ? values[values.Length - 1] : 0;
        NoStroke();
        Fill(110, 220, 160, 60 + latest * 195);
        Circle(Width - 120, Height / 2f, 40 + latest * 150);
        Fill(255);
        TextSize(28);
        TextAlign(TextAlignH.Center);
        Text(((int)(latest * 1023)).ToString(), Width - 120, Height / 2f + 10);
        TextAlign(TextAlignH.Left);

        Fill(150);
        TextSize(13);
        Text(_status + (IsMousePressed ? ""   |   LED on"" : ""   |   hold the mouse to light the LED""), 20, Height - 20);
    }

    public override void MousePressed() => Send(""1"");
    public override void MouseReleased() => Send(""0"");

    private void Send(string text)
    {
        try
        {
            if (_port != null && _port.IsOpen)
                _port.Write(text);
        }
        catch { /* unplugged mid-run: just stop sending */ }
    }

    public override void OnStop()
    {
        var port = _port;
        _port = null;
        try
        {
            port?.Close();
            port?.Dispose();
        }
        catch { }
    }
}";

        private const string WebcamSlitScan =
@"// nuget: FlashCap, 1.12.0

using System;
using System.Collections.Generic;
using System.Linq;
using FlashCap;

// Your webcam, three ways -- with FlashCap, a camera library with no native
// dependencies (Windows: DirectShow / Video for Windows, Linux: V4L2).
//   1  a mirror
//   2  slit-scan: every horizontal band is a different moment, the top is
//      now and the bottom is about a second ago -- move and you smear in time
//   3  echoes: tinted ghosts of the last second, on top of the present
//
// The camera hands frames over on its own thread; Draw() just decodes the
// newest one with LoadImage(byte[]) and keeps the last 48 for the effects.
// OnStop() releases the camera, so the next Run (or another app) can use it.
public class MySketch : Sketch
{
    private const int History = 48;

    private CaptureDevice? _device;
    private byte[]? _incoming; // newest encoded frame, written by the camera thread
    private readonly object _lock = new object();
    private readonly List<PImage> _frames = new List<PImage>(); // newest last
    private volatile bool _stopping;
    private int _mode = 2;
    private string _status = ""Opening the camera..."";

    public override void Setup()
    {
        Size(960, 720);
        Thread(nameof(OpenCamera)); // opening a camera can take a second: don't block Setup()
    }

    private void OpenCamera()
    {
        try
        {
            var descriptor = new CaptureDevices()
                .EnumerateDescriptors()
                .FirstOrDefault(d => d.Characteristics.Length > 0);
            if (descriptor == null)
            {
                _status = ""No camera found"";
                return;
            }

            // Something near 640 wide that FlashCap knows how to decode.
            var characteristics = descriptor.Characteristics
                .Where(c => c.PixelFormat != PixelFormats.Unknown)
                .OrderBy(c => Math.Abs(c.Width - 640))
                .FirstOrDefault();
            if (characteristics == null)
            {
                _status = ""The camera offers no format FlashCap can decode"";
                return;
            }

            var device = descriptor.OpenAsync(characteristics, scope =>
            {
                byte[] image = scope.Buffer.CopyImage(); // a copy: safe to use after this callback
                lock (_lock)
                    _incoming = image;
            }).GetAwaiter().GetResult();
            device.StartAsync().GetAwaiter().GetResult();

            if (_stopping) // the sketch was stopped while the camera was still opening
            {
                device.StopAsync().GetAwaiter().GetResult();
                device.Dispose();
                return;
            }
            _device = device;
            _status = $""{descriptor.Name}  {characteristics}"";
        }
        catch (Exception ex)
        {
            _status = ""Camera error: "" + ex.Message;
        }
    }

    public override void Draw()
    {
        Background(0);

        byte[]? bytes;
        lock (_lock)
        {
            bytes = _incoming;
            _incoming = null;
        }
        if (bytes != null)
        {
            try
            {
                _frames.Add(LoadImage(bytes));
                while (_frames.Count > History)
                {
                    _frames[0].Dispose();
                    _frames.RemoveAt(0);
                }
            }
            catch { /* the odd corrupt frame: skip it */ }
        }

        if (_frames.Count == 0)
        {
            Fill(200);
            TextSize(16);
            Text(_status, 24, Height / 2f);
            return;
        }

        // Flipped horizontally, like a mirror.
        PushMatrix();
        Translate(Width, 0);
        Scale(-1, 1);
        if (_mode == 1)
            Image(_frames[_frames.Count - 1], 0, 0, Width, Height);
        else if (_mode == 2)
            SlitScan();
        else
            Echoes();
        PopMatrix();

        Fill(0, 0, 0, 150);
        Rect(0, Height - 34, Width, 34);
        Fill(230);
        TextSize(13);
        Text($""1 mirror   2 slit-scan   3 echoes   |   {_status}"", 14, Height - 12);
    }

    // Band i shows the frame from i frames ago.
    private void SlitScan()
    {
        int bands = _frames.Count;
        float bandHeight = Height / (float)bands;
        for (int i = 0; i < bands; i++)
        {
            Clip(0, i * bandHeight, Width, bandHeight + 1);
            Image(_frames[_frames.Count - 1 - i], 0, 0, Width, Height);
        }
        NoClip();
    }

    private void Echoes()
    {
        Image(_frames[_frames.Count - 1], 0, 0, Width, Height);
        for (int k = 1; k <= 5; k++)
        {
            int index = _frames.Count - 1 - k * 8;
            if (index < 0)
                break;
            Tint(255, 120 + k * 20, 255 - k * 30, 70);
            Image(_frames[index], 0, 0, Width, Height);
        }
        NoTint();
    }

    public override void KeyPressed()
    {
        if (Key >= '1' && Key <= '3')
            _mode = Key - '0';
    }

    public override void OnStop()
    {
        _stopping = true;
        var device = _device;
        _device = null;
        try
        {
            device?.StopAsync().GetAwaiter().GetResult();
            device?.Dispose();
        }
        catch { }

        foreach (var frame in _frames)
            frame.Dispose();
        _frames.Clear();
    }
}";

        private const string MidiBounce =
@"// nuget: NAudio, 2.2.1

using System;
using System.Collections.Generic;
using NAudio.Midi;

// Balls bouncing in a box -- every wall they hit plays a note through MIDI
// output 0, from a minor pentatonic scale (so it always sounds good). On
// Windows output 0 is the built-in ""Microsoft GS Wavetable Synth"", so it plays
// with no setup at all; with a virtual port (loopMIDI) or a USB synth it plays
// your DAW or hardware instead. NAudio's MIDI is Windows-only.
//
// Click to add a ball, right-click to remove one. The [Tweak] panel changes
// gravity, the root note, the instrument and how long notes ring -- live.
// OnStop() silences anything still sounding and releases the MIDI port.
public class MySketch : Sketch
{
    private static readonly int[] Pentatonic = { 0, 3, 5, 7, 10, 12, 15, 17 };

    [Tweak(0, 0.6f)] float gravity = 0.18f;
    [Tweak(36, 72)] int rootNote = 48;
    [Tweak(0, 127, Label = ""instrument (GM)"")] int program = 11; // 11 = vibraphone
    [Tweak(50, 2000, Label = ""note ms"")] int noteLength = 450;

    private MidiOut? _midi;
    private string _status = """";
    private int _sentProgram = -1;
    private readonly List<Ball> _balls = new List<Ball>();
    private readonly List<(int Note, long OffAt)> _sounding = new List<(int Note, long OffAt)>();

    private sealed class Ball
    {
        public float X, Y, VX, VY, Radius, Flash;
        public int Degree;
    }

    public override void Setup()
    {
        Size(820, 560);
        try
        {
            if (MidiOut.NumberOfDevices > 0)
            {
                _midi = new MidiOut(0);
                _status = ""MIDI out: "" + MidiOut.DeviceInfo(0).ProductName;
            }
            else
            {
                _status = ""No MIDI output found -- silent"";
            }
        }
        catch (Exception ex)
        {
            _status = ""MIDI unavailable ("" + ex.GetType().Name + "") -- silent"";
        }

        for (int i = 0; i < 4; i++)
            AddBall(Random(100, Width - 100), Random(80, Height / 2f));
    }

    private void AddBall(float x, float y)
    {
        _balls.Add(new Ball
        {
            X = x,
            Y = y,
            VX = Random(-5, 5),
            VY = Random(-2, 2),
            Radius = Random(14, 30),
            Degree = _balls.Count % Pentatonic.Length,
        });
    }

    public override void Draw()
    {
        Background(14, 16, 24, 90); // translucent: leaves trails

        if (program != _sentProgram)
        {
            Send(MidiMessage.ChangePatch(Constrain(program, 0, 127), 1));
            _sentProgram = program;
        }

        foreach (var ball in _balls)
        {
            ball.VY += gravity;
            ball.X += ball.VX;
            ball.Y += ball.VY;

            bool hit = false;
            if (ball.X < ball.Radius || ball.X > Width - ball.Radius)
            {
                ball.VX = -ball.VX;
                ball.X = Constrain(ball.X, ball.Radius, Width - ball.Radius);
                hit = true;
            }
            if (ball.Y > Height - ball.Radius)
            {
                ball.VY = -Math.Abs(ball.VY) * 0.98f;
                ball.Y = Height - ball.Radius;
                hit = true;
            }
            if (ball.Y < ball.Radius)
            {
                ball.VY = Math.Abs(ball.VY);
                ball.Y = ball.Radius;
                hit = true;
            }

            if (hit)
            {
                // Higher on screen = higher note; bigger ball = louder.
                int degree = (ball.Degree + (int)Map(ball.Y, Height, 0, 0, 4)) % Pentatonic.Length;
                PlayNote(rootNote + Pentatonic[degree], (int)Map(ball.Radius, 14, 30, 60, 120));
                ball.Flash = 1;
            }

            ball.Flash *= 0.9f;
            NoStroke();
            FillHSB(ball.Degree * 45 % 360, 60, 60 + ball.Flash * 40); // h 0-360, s and b 0-100
            Circle(ball.X, ball.Y, ball.Radius * 2 * (1 + ball.Flash * 0.3f));
        }

        ReleaseFinishedNotes();

        Fill(170);
        TextSize(13);
        Text($""{_status}   |   {_balls.Count} balls   |   click: add   right-click: remove"", 16, Height - 16);
    }

    private void PlayNote(int note, int velocity)
    {
        note = Constrain(note, 0, 127);
        Send(MidiMessage.StartNote(note, Constrain(velocity, 1, 127), 1));
        _sounding.Add((note, Millis() + noteLength));
    }

    private void ReleaseFinishedNotes()
    {
        long now = Millis();
        for (int i = _sounding.Count - 1; i >= 0; i--)
        {
            if (_sounding[i].OffAt > now)
                continue;
            Send(MidiMessage.StopNote(_sounding[i].Note, 0, 1));
            _sounding.RemoveAt(i);
        }
    }

    private void Send(MidiMessage message)
    {
        try { _midi?.Send(message.RawData); }
        catch { /* device unplugged: keep drawing, just silently */ }
    }

    public override void MousePressed()
    {
        if (MouseButton == MouseButtonKind.Right)
        {
            if (_balls.Count > 0)
                _balls.RemoveAt(_balls.Count - 1);
        }
        else
        {
            AddBall(MouseX, MouseY);
        }
    }

    public override void OnStop()
    {
        foreach (var (note, _) in _sounding)
            Send(MidiMessage.StopNote(note, 0, 1));
        _sounding.Clear();
        _midi?.Dispose();
        _midi = null;
    }
}";

        private const string LiveShader =
@"// Live shaders, Shadertoy-style -- a fragment shader in SkSL (Skia's GLSL:
// half4/float2 instead of vec4/vec2) painting every pixel, fed by the sound
// engine. Shade(source) compiles it once and caches it by its text, so:
//
//   change a number inside the shader below and press ⚡ Hot Reload (or save)
//   -> the new shader takes over instantly, time and music keep going.
//   A typo doesn't crash anything: the last good shader keeps running and the
//   compiler's message shows bottom-left.
//
// Uniforms filled in for you when declared: iTime, iResolution, iMouse
// (x, y, pressed), iFrame, iAmplitude and iSpectrum[8/16/32/64] -- here the
// bass of the beat below pushes the rings and the loudness lights the center.
// Runs on the CPU in the 2D renderer, every OS: keep the canvas modest (and
// try a lower supersampling in Settings if it gets heavy).
public class MySketch : Sketch
{
    const string Plasma = @""
uniform float iTime;
uniform float2 iResolution;
uniform float4 iMouse;
uniform float iAmplitude;
uniform float iSpectrum[16];

half4 main(float2 fragCoord) {
    float2 uv = (fragCoord - 0.5 * iResolution) / iResolution.y;
    float2 m = (iMouse.xy - 0.5 * iResolution) / iResolution.y;
    float d = length(uv - m * 0.6);
    float bass = iSpectrum[1] + iSpectrum[2];

    float v = sin(12.0 * d - iTime * 3.0 + bass * 5.0)
            + sin(7.0 * uv.x + iTime)
            + sin(6.0 * (uv.x + uv.y) - iTime * 1.3);
    v /= 3.0;

    float3 col = 0.5 + 0.5 * cos(6.2831 * (v + float3(0.0, 0.33, 0.67)) + iTime * 0.2);
    col += iAmplitude * 0.8 * exp(-d * 5.0);
    return half4(half3(col), 1.0);
}"";

    public override void Setup()
    {
        Size(720, 450);
    }

    public override void Draw()
    {
        Shade(Plasma);
    }

    [LiveLoop]
    async Task Beat()
    {
        Sample(""kick"");
        await Sleep(0.5);
        Sample(""hat"");
        await Sleep(0.5);
        Sample(""snare"");
        await Sleep(0.5);
        Sample(""hat"");
        await Sleep(0.5);
    }

    [LiveLoop]
    async Task Bass()
    {
        UseSynth(Synth.Saw);
        foreach (var note in new[] { ""E2"", ""E2"", ""G2"", ""D2"" })
        {
            Play(note, release: 0.4, cutoff: 70);
            await Sleep(1);
        }
    }
}";

    }
}
