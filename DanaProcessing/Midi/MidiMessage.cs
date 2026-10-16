namespace DanaProcessing
{
    /// <summary>What a <see cref="MidiMessage"/> is.</summary>
    public enum MidiKind
    {
        NoteOn,
        NoteOff,
        ControlChange,
        ProgramChange,
        PitchBend,
        ChannelPressure,
        PolyPressure,
        Other,
    }

    /// <summary>
    /// One MIDI message from a controller -- what <see cref="Sketch.OnMidi"/>
    /// receives. <see cref="Channel"/> is 1..16, like the labels on hardware.
    /// A Note On with velocity 0 is reported as <see cref="MidiKind.NoteOff"/>
    /// (that's how most keyboards send note-offs).
    /// <code>
    /// public override void OnMidi(MidiMessage m)
    /// {
    ///     if (m.Kind == MidiKind.ControlChange &amp;&amp; m.Controller == 21)
    ///         size = m.Value01 * 300;
    /// }
    /// </code>
    /// </summary>
    public readonly record struct MidiMessage(byte Status, byte Data1, byte Data2, string Device = "")
    {
        public MidiKind Kind => (Status & 0xF0) switch
        {
            0x90 => Data2 == 0 ? MidiKind.NoteOff : MidiKind.NoteOn,
            0x80 => MidiKind.NoteOff,
            0xB0 => MidiKind.ControlChange,
            0xC0 => MidiKind.ProgramChange,
            0xE0 => MidiKind.PitchBend,
            0xD0 => MidiKind.ChannelPressure,
            0xA0 => MidiKind.PolyPressure,
            _ => MidiKind.Other,
        };

        /// <summary>1..16.</summary>
        public int Channel => (Status & 0x0F) + 1;

        /// <summary>Note number (60 = C4) for notes.</summary>
        public int Note => Data1;

        /// <summary>0..127 for notes.</summary>
        public int Velocity => Data2;

        /// <summary>CC number for a ControlChange (a knob or fader).</summary>
        public int Controller => Data1;

        /// <summary>The CC value (0..127), or the program for ProgramChange / pressure for ChannelPressure.</summary>
        public int Value => Kind is MidiKind.ProgramChange or MidiKind.ChannelPressure ? Data1 : Data2;

        /// <summary><see cref="Value"/> as 0..1, ready to use as a size, a speed or a mix.</summary>
        public float Value01 => Value / 127f;

        /// <summary>-8192..8191 for PitchBend (0 = centred).</summary>
        public int Bend => ((Data2 << 7) | Data1) - 8192;

        public override string ToString() => Kind switch
        {
            MidiKind.NoteOn => $"NoteOn {Note} vel {Velocity} ch {Channel}",
            MidiKind.NoteOff => $"NoteOff {Note} ch {Channel}",
            MidiKind.ControlChange => $"CC {Controller} = {Value} ch {Channel}",
            MidiKind.ProgramChange => $"Program {Value} ch {Channel}",
            MidiKind.PitchBend => $"PitchBend {Bend} ch {Channel}",
            MidiKind.ChannelPressure => $"Pressure {Value} ch {Channel}",
            MidiKind.PolyPressure => $"PolyPressure {Note} = {Data2} ch {Channel}",
            _ => $"MIDI 0x{Status:X2} {Data1} {Data2}",
        };
    }
}

namespace DanaProcessing.Midi
{
    /// <summary>
    /// Turns a raw MIDI byte stream (what ALSA rawmidi and CoreMIDI packets
    /// deliver) into channel messages: handles running status, skips SysEx and
    /// system common messages, and lets real-time bytes (clock, start/stop)
    /// pass through the middle of a message without breaking it -- they are
    /// dropped, as nothing in DanaProcessing uses them yet.
    /// </summary>
    internal sealed class MidiStreamParser
    {
        private readonly string _device;
        private byte _status;      // running status (0 = none)
        private byte _first;       // first data byte, when waiting for the second
        private bool _haveFirst;
        private bool _inSysEx;
        private int _skip;         // data bytes of a system common message still to skip

        public MidiStreamParser(string device = "") => _device = device;

        public void Feed(ReadOnlySpan<byte> bytes, Action<MidiMessage> emit)
        {
            foreach (byte b in bytes)
                Feed(b, emit);
        }

        public void Feed(byte b, Action<MidiMessage> emit)
        {
            if (b >= 0xF8)
                return; // real-time: may appear anywhere, never changes the parser's state

            if (b >= 0x80)
            {
                _haveFirst = false;
                _skip = 0;
                if (b == 0xF0)
                {
                    _inSysEx = true;
                    _status = 0;
                    return;
                }
                if (b == 0xF7)
                {
                    _inSysEx = false;
                    return;
                }
                _inSysEx = false;
                if (b >= 0xF0)
                {
                    // System common: cancels running status; skip its data bytes.
                    _status = 0;
                    _skip = b switch { 0xF1 => 1, 0xF2 => 2, 0xF3 => 1, _ => 0 };
                    return;
                }
                _status = b;
                return;
            }

            // Data byte.
            if (_inSysEx)
                return;
            if (_skip > 0)
            {
                _skip--;
                return;
            }
            if (_status == 0)
                return; // stray data byte with no status to apply it to

            int needed = (_status & 0xF0) is 0xC0 or 0xD0 ? 1 : 2;
            if (needed == 1)
            {
                emit(new MidiMessage(_status, b, 0, _device));
                return;
            }
            if (!_haveFirst)
            {
                _first = b;
                _haveFirst = true;
                return;
            }
            _haveFirst = false;
            emit(new MidiMessage(_status, _first, b, _device));
        }
    }
}
