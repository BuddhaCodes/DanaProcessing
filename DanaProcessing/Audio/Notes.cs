using System;
using System.Collections.Generic;
using System.Globalization;

namespace DanaProcessing
{
    /// <summary>
    /// The built-in instruments a live loop can play with Play(...) or
    /// Sample(...). Every one of these is synthesized by DanaProcessing's own
    /// audio engine (no external synth, no sample files needed).
    /// </summary>
    public enum Synth
    {
        /// <summary>Pure sine wave -- the default, like Sonic Pi's :beep.</summary>
        Sine,
        /// <summary>Band-limited sawtooth: bright, good for bass and leads (try it with cutoff).</summary>
        Saw,
        /// <summary>Band-limited square: hollow, retro.</summary>
        Square,
        /// <summary>Triangle: soft, flute-like.</summary>
        Triangle,
        /// <summary>Plucked string (Karplus-Strong).</summary>
        Pluck,
        /// <summary>White noise -- with a low cutoff it becomes wind/ocean.</summary>
        Noise,
        /// <summary>Kick drum (also Sample("kick") / "bd").</summary>
        Kick,
        /// <summary>Snare drum (also Sample("snare") / "sn").</summary>
        Snare,
        /// <summary>Closed hi-hat (also Sample("hat") / "hh").</summary>
        Hat,
    }

    /// <summary>
    /// Music-theory helpers: note names to MIDI numbers, chords and scales.
    /// MIDI convention matches Sonic Pi: "C4" = 60, "A4" = 69 (440 Hz).
    /// Lives in its own static class (Notes.Chord, Notes.Scale) rather than
    /// on Sketch because Sketch already has a Scale() -- the transform one.
    /// </summary>
    public static class Notes
    {
        private static readonly Dictionary<string, int[]> ChordShapes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["major"] = new[] { 0, 4, 7 },
            ["minor"] = new[] { 0, 3, 7 },
            ["dim"] = new[] { 0, 3, 6 },
            ["aug"] = new[] { 0, 4, 8 },
            ["7"] = new[] { 0, 4, 7, 10 },
            ["maj7"] = new[] { 0, 4, 7, 11 },
            ["m7"] = new[] { 0, 3, 7, 10 },
            ["minor7"] = new[] { 0, 3, 7, 10 },
            ["sus2"] = new[] { 0, 2, 7 },
            ["sus4"] = new[] { 0, 5, 7 },
            ["power"] = new[] { 0, 7 },
        };

        private static readonly Dictionary<string, int[]> ScaleShapes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["major"] = new[] { 0, 2, 4, 5, 7, 9, 11 },
            ["minor"] = new[] { 0, 2, 3, 5, 7, 8, 10 },
            ["harmonic_minor"] = new[] { 0, 2, 3, 5, 7, 8, 11 },
            ["dorian"] = new[] { 0, 2, 3, 5, 7, 9, 10 },
            ["phrygian"] = new[] { 0, 1, 3, 5, 7, 8, 10 },
            ["lydian"] = new[] { 0, 2, 4, 6, 7, 9, 11 },
            ["mixolydian"] = new[] { 0, 2, 4, 5, 7, 9, 10 },
            ["major_pentatonic"] = new[] { 0, 2, 4, 7, 9 },
            ["pentatonic"] = new[] { 0, 2, 4, 7, 9 },
            ["minor_pentatonic"] = new[] { 0, 3, 5, 7, 10 },
            ["blues"] = new[] { 0, 3, 5, 6, 7, 10 },
            ["chromatic"] = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
        };

        /// <summary>
        /// "C4" -> 60, "F#3" -> 54, "Eb2" -> 39, "a" -> 69 (octave defaults to 4).
        /// Sharps: '#' or 's'. Flats: 'b'. Also accepts a plain number ("60").
        /// </summary>
        public static int Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Notes.Parse(): el nombre de la nota está vacío.", nameof(name));

            var s = name.Trim();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var midi))
                return midi;

            int semitone = char.ToUpperInvariant(s[0]) switch
            {
                'C' => 0,
                'D' => 2,
                'E' => 4,
                'F' => 5,
                'G' => 7,
                'A' => 9,
                'B' => 11,
                _ => throw new ArgumentException($"Notes.Parse(): \"{name}\" no es una nota válida (usá C, D, E, F, G, A o B, ej. \"C4\", \"F#3\", \"Eb2\").", nameof(name)),
            };

            int i = 1;
            while (i < s.Length && (s[i] == '#' || s[i] == 's' || s[i] == 'b'))
            {
                semitone += s[i] == 'b' ? -1 : 1;
                i++;
            }

            int octave = 4;
            if (i < s.Length && !int.TryParse(s.AsSpan(i), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out octave))
                throw new ArgumentException($"Notes.Parse(): no entiendo la octava en \"{name}\".", nameof(name));

            return (octave + 1) * 12 + semitone;
        }

        /// <summary>MIDI note number (fractional allowed, for microtones) to frequency in Hz.</summary>
        public static double ToFrequency(double midi) => 440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0);

        /// <summary>Notes of a chord: Notes.Chord("C4", "major") -> [60, 64, 67].
        /// Kinds: major, minor, dim, aug, 7, maj7, m7, sus2, sus4, power.</summary>
        public static int[] Chord(string root, string kind = "major") => Chord(Parse(root), kind);

        /// <inheritdoc cref="Chord(string, string)"/>
        public static int[] Chord(int root, string kind = "major")
        {
            var shape = Lookup(ChordShapes, kind, "Chord");
            var result = new int[shape.Length];
            for (int i = 0; i < shape.Length; i++)
                result[i] = root + shape[i];
            return result;
        }

        /// <summary>
        /// Notes of a scale, ending on the root an octave (or more) up, like
        /// Sonic Pi: Notes.Scale("E2", "minor_pentatonic") -> [40, 43, 45, 47, 50, 52].
        /// Kinds: major, minor, harmonic_minor, dorian, phrygian, lydian,
        /// mixolydian, major_pentatonic, minor_pentatonic, blues, chromatic.
        /// </summary>
        public static int[] Scale(string root, string kind = "major", int octaves = 1) => Scale(Parse(root), kind, octaves);

        /// <inheritdoc cref="Scale(string, string, int)"/>
        public static int[] Scale(int root, string kind = "major", int octaves = 1)
        {
            if (octaves < 1)
                throw new ArgumentOutOfRangeException(nameof(octaves), "Notes.Scale(): octaves tiene que ser 1 o más.");

            var shape = Lookup(ScaleShapes, kind, "Scale");
            var result = new int[shape.Length * octaves + 1];
            int k = 0;
            for (int o = 0; o < octaves; o++)
                foreach (var step in shape)
                    result[k++] = root + o * 12 + step;
            result[k] = root + octaves * 12;
            return result;
        }

        private static int[] Lookup(Dictionary<string, int[]> table, string kind, string what)
        {
            if (table.TryGetValue(kind, out var shape))
                return shape;
            throw new ArgumentException(
                $"Notes.{what}(): no conozco \"{kind}\". Opciones: {string.Join(", ", table.Keys)}.",
                nameof(kind));
        }
    }
}
