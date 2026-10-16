using System;
using System.Collections.Generic;

namespace DanaProcessing.Midi
{
    /// <summary>
    /// The process-wide MIDI input: opens every MIDI input the OS reports (only
    /// once some sketch actually uses MIDI -- on Windows an open input is busy
    /// for other apps), merges what they send, and keeps it in a small ring
    /// buffer that every sketch reads at its own pace with its own cursor, so the
    /// IDE's sketch and a preview never steal messages from each other.
    /// Also keeps the latest value of every CC and which notes are held, for
    /// <see cref="Sketch.MidiCC"/> and <see cref="Sketch.MidiNoteDown"/>.
    ///
    /// Backends call <see cref="Push"/> from their own threads; sketches read on
    /// the drawing thread (Sketch.DispatchMidi, right before Draw()).
    /// </summary>
    internal static class MidiHub
    {
        private const int RingSize = 1024;

        private static readonly object Gate = new();
        private static readonly MidiMessage[] Ring = new MidiMessage[RingSize];
        private static long _written;

        // [channel 0..15, number 0..127]; index 16 = "any channel" (the latest seen on any).
        private static readonly float[,] Cc = new float[17, 128];
        private static readonly byte[,] NoteVelocity = new byte[17, 128];

        private static IMidiBackend? _backend;
        private static IReadOnlyList<string> _devices = Array.Empty<string>();
        private static bool _opened;

        /// <summary>Tests turn real devices off so a CI machine's (missing) MIDI stack never matters.</summary>
        internal static bool DevicesEnabled { get; set; } = true;

        /// <summary>Total messages received so far; a sketch's cursor starts here so it never replays old ones.</summary>
        public static long Written
        {
            get { lock (Gate) return _written; }
        }

        /// <summary>Names of the MIDI inputs that are open (empty until something uses MIDI, or with no devices).</summary>
        public static IReadOnlyList<string> Devices
        {
            get { lock (Gate) return _devices; }
        }

        /// <summary>True once the inputs have been opened (whether or not there were any).</summary>
        public static bool IsOpen
        {
            get { lock (Gate) return _opened; }
        }

        /// <summary>Opens every MIDI input, once. Never throws: no MIDI stack just means no devices.</summary>
        public static void EnsureOpen()
        {
            lock (Gate)
            {
                if (_opened)
                    return;
                _opened = true;
            }
            OpenDevices();
        }

        /// <summary>Closes and reopens every input -- picks up a controller plugged in after the first use.</summary>
        public static void Refresh()
        {
            IMidiBackend? old;
            lock (Gate)
            {
                old = _backend;
                _backend = null;
                _devices = Array.Empty<string>();
                _opened = true;
            }
            try { old?.Dispose(); } catch { /* closing a vanished device can fail; it's gone either way */ }
            OpenDevices();
        }

        private static void OpenDevices()
        {
            if (!DevicesEnabled)
                return;

            IMidiBackend? backend = null;
            IReadOnlyList<string> names = Array.Empty<string>();
            try
            {
                backend = MidiBackends.Create();
                if (backend != null)
                    names = backend.Open(Push);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or BadImageFormatException)
            {
                DanaLogger.Warn($"MIDI: no se pudo abrir la entrada MIDI ({ex.Message}).");
                try { backend?.Dispose(); } catch { }
                backend = null;
            }

            lock (Gate)
            {
                _backend = backend;
                _devices = names;
            }

            DanaLogger.Info(names.Count == 0
                ? "MIDI: no hay entradas MIDI conectadas."
                : "MIDI: " + string.Join(", ", names));
        }

        /// <summary>Records one incoming message. Any thread.</summary>
        public static void Push(MidiMessage m)
        {
            lock (Gate)
            {
                int ch = m.Channel - 1;
                switch (m.Kind)
                {
                    case MidiKind.ControlChange:
                        Cc[ch, m.Controller] = Cc[16, m.Controller] = m.Data2 / 127f;
                        break;
                    case MidiKind.NoteOn:
                        NoteVelocity[ch, m.Note] = NoteVelocity[16, m.Note] = m.Data2;
                        break;
                    case MidiKind.NoteOff:
                        NoteVelocity[ch, m.Note] = NoteVelocity[16, m.Note] = 0;
                        break;
                }
                Ring[_written % RingSize] = m;
                _written++;
            }
        }

        /// <summary>
        /// Copies every message after <paramref name="cursor"/> into
        /// <paramref name="into"/> and moves the cursor to the end. A reader that
        /// fell more than the ring's size behind skips the oldest ones.
        /// </summary>
        public static void Read(ref long cursor, List<MidiMessage> into)
        {
            lock (Gate)
            {
                if (cursor > _written)
                    cursor = _written; // the hub was reset under us
                if (_written - cursor > RingSize)
                    cursor = _written - RingSize;
                for (; cursor < _written; cursor++)
                    into.Add(Ring[cursor % RingSize]);
            }
        }

        /// <summary>Latest value (0..1) of a CC; channel 1..16, or 0 for any channel.</summary>
        public static float ControlValue(int controller, int channel)
        {
            if (controller < 0 || controller > 127)
                return 0f;
            int ch = channel is >= 1 and <= 16 ? channel - 1 : 16;
            lock (Gate)
                return Cc[ch, controller];
        }

        /// <summary>Velocity (1..127) of a held note, 0 when it's up; channel 1..16, or 0 for any.</summary>
        public static int NoteDownVelocity(int note, int channel)
        {
            if (note < 0 || note > 127)
                return 0;
            int ch = channel is >= 1 and <= 16 ? channel - 1 : 16;
            lock (Gate)
                return NoteVelocity[ch, note];
        }

        /// <summary>Tests: forget every message and value (devices stay as they are).</summary>
        internal static void ResetState()
        {
            lock (Gate)
            {
                Array.Clear(Ring);
                Array.Clear(Cc);
                Array.Clear(NoteVelocity);
                _written = 0;
            }
        }
    }
}
