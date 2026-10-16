using System.Collections.Concurrent;
using DanaProcessing.Midi;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // MIDI input -- knobs, faders, pads and keys from a controller. The
        // engine opens every MIDI input the first time a sketch uses MIDI
        // (overrides OnMidi, calls MidiCC/MidiNoteDown/MidiInputs/MapMidi, or
        // turns on MIDI learn in the [Tweak] panel) and delivers messages on the
        // drawing thread, right before Draw() -- like OnNote().
        // =====================================================================

        private static readonly ConcurrentDictionary<Type, bool> OverridesOnMidi = new();

        // Per-sketch cursor into the shared message ring (see MidiHub). Nullable
        // so a sketch created without its constructor running (hot reload tests)
        // still starts at "now" instead of replaying every message ever received.
        private long? _midiCursor;
        private List<MidiMessage>? _midiScratch;
        private bool _usesMidi;

        /// <summary>
        /// Called for every MIDI message from any connected controller, on the
        /// drawing thread right before Draw(). <c>m.Kind</c> says what it is
        /// (NoteOn, NoteOff, ControlChange...), <c>m.Value01</c> is a knob or
        /// fader as 0..1.
        /// </summary>
        public virtual void OnMidi(MidiMessage m) { }

        /// <summary>
        /// Latest position of knob/fader <paramref name="controller"/> (its CC
        /// number) as 0..1 -- 0 until it first moves. <paramref name="channel"/>
        /// is 1..16, or 0 for whichever channel moved it last.
        /// </summary>
        public float MidiCC(int controller, int channel = 0)
        {
            UseMidi();
            return MidiHub.ControlValue(controller, channel);
        }

        /// <summary>True while <paramref name="note"/> (60 = C4) is held on a MIDI keyboard or pad.</summary>
        public bool MidiNoteDown(int note, int channel = 0)
        {
            UseMidi();
            return MidiHub.NoteDownVelocity(note, channel) > 0;
        }

        /// <summary>Velocity (1..127) the held <paramref name="note"/> was hit with, 0 when it's up.</summary>
        public int MidiNoteVelocity(int note, int channel = 0)
        {
            UseMidi();
            return MidiHub.NoteDownVelocity(note, channel);
        }

        /// <summary>Names of the connected MIDI inputs (opens them if nothing has yet).</summary>
        public string[] MidiInputs()
        {
            UseMidi();
            return MidiHub.Devices.ToArray();
        }

        /// <summary>Closes and reopens every MIDI input -- call it after plugging in a controller while the sketch runs.</summary>
        public void RefreshMidi() => MidiHub.Refresh();

        /// <summary>
        /// Connects knob/fader <paramref name="controller"/> to the [Tweak] field
        /// <paramref name="field"/> from code -- the same thing MIDI learn in the
        /// panel does, but it stays in your sketch. The knob's 0..127 maps onto
        /// the slider's range; for a bool, 64 and up is true. For a Color field,
        /// pick the channel: "bg.r", "bg.g" or "bg.b".
        /// </summary>
        public void MapMidi(string field, int controller, int channel = 0)
        {
            UseMidi();
            Tweaks.Bind(this, field, new MidiBinding(false, controller, channel));
        }

        /// <summary>Connects a pad/key to a [Tweak] field: a bool toggles on each hit, a number jumps to the hit's velocity.</summary>
        public void MapMidiNote(string field, int note, int channel = 0)
        {
            UseMidi();
            Tweaks.Bind(this, field, new MidiBinding(true, note, channel));
        }

        private void UseMidi()
        {
            if (_usesMidi || IgnoresAudio)
                return; // previews and the variations sheet never open devices
            _usesMidi = true;
            MidiHub.EnsureOpen();
        }

        /// <summary>
        /// Hands every MIDI message received since the last frame to the [Tweak]
        /// panel (MIDI learn and bound knobs) and then to OnMidi(). Cheap no-op
        /// for sketches that never touch MIDI.
        /// </summary>
        internal void DispatchMidi()
        {
            if (IgnoresAudio)
                return;
            if (!_usesMidi)
            {
                bool overrides = OverridesOnMidi.GetOrAdd(GetType(), t =>
                    t.GetMethod(nameof(OnMidi), new[] { typeof(MidiMessage) })?.DeclaringType != typeof(Sketch));
                if (!overrides && !Tweaks.WantsMidi(this))
                {
                    // Keep the cursor current, so a sketch that starts using MIDI
                    // later (turning on MIDI learn, say) gets what arrives from
                    // that frame on -- including the knob that's being learned.
                    _midiCursor = MidiHub.Written;
                    return;
                }
                UseMidi();
            }

            long cursor = _midiCursor ?? MidiHub.Written;
            var batch = _midiScratch ??= new List<MidiMessage>();
            batch.Clear();
            MidiHub.Read(ref cursor, batch);
            _midiCursor = cursor;

            foreach (var m in batch)
            {
                Tweaks.OnMidi(this, m);
                OnMidi(m);
            }
        }
    }
}
