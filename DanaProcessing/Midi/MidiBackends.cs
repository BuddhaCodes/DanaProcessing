using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DanaProcessing.Midi
{
    /// <summary>One OS's MIDI input API: opens every input and feeds what they send to a sink.</summary>
    internal interface IMidiBackend : IDisposable
    {
        /// <summary>Opens every input it can find; returns their names. <paramref name="sink"/> may be called from any thread.</summary>
        IReadOnlyList<string> Open(Action<MidiMessage> sink);
    }

    /// <summary>
    /// MIDI input with no dependencies, the same way the audio engine talks to
    /// the sound card: P/Invoke straight into what every OS already has --
    /// WinMM on Windows, CoreMIDI on macOS, ALSA rawmidi on Linux.
    /// </summary>
    internal static class MidiBackends
    {
        public static IMidiBackend? Create()
        {
            if (OperatingSystem.IsWindows())
                return new WinMmMidiInput();
            if (OperatingSystem.IsMacOS())
                return new CoreMidiInput();
            if (OperatingSystem.IsLinux())
                return new AlsaRawMidiInput();
            return null;
        }
    }

    /// <summary>Windows: winmm's midiIn* -- every USB controller with a class-compliant driver shows up here.</summary>
    internal sealed class WinMmMidiInput : IMidiBackend
    {
        private const uint CALLBACK_FUNCTION = 0x00030000;
        private const uint MIM_DATA = 0x3C3;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MidiInCaps
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwSupport;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void MidiInProc(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2);

        [DllImport("winmm.dll")] private static extern uint midiInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern uint midiInGetDevCapsW(UIntPtr deviceId, ref MidiInCaps caps, uint size);
        [DllImport("winmm.dll")] private static extern uint midiInOpen(out IntPtr handle, uint deviceId, MidiInProc callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint midiInStart(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiInStop(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiInReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiInClose(IntPtr handle);

        private readonly MidiInProc _callback; // kept alive as long as the devices are open
        private readonly List<IntPtr> _handles = new();
        private readonly List<string> _names = new();
        private Action<MidiMessage>? _sink;

        public WinMmMidiInput() => _callback = OnMessage;

        public IReadOnlyList<string> Open(Action<MidiMessage> sink)
        {
            _sink = sink;
            uint count = midiInGetNumDevs();
            for (uint i = 0; i < count; i++)
            {
                var caps = new MidiInCaps();
                string name = midiInGetDevCapsW((UIntPtr)i, ref caps, (uint)Marshal.SizeOf<MidiInCaps>()) == 0
                    ? caps.szPname
                    : $"MIDI {i}";

                // The instance value is the index into _names, so the callback knows the device.
                if (midiInOpen(out var handle, i, _callback, (IntPtr)_names.Count, CALLBACK_FUNCTION) != 0)
                {
                    DanaLogger.Warn($"MIDI: '{name}' está en uso por otro programa (o no se pudo abrir).");
                    continue;
                }
                _names.Add(name);
                _handles.Add(handle);
                midiInStart(handle);
            }
            return _names.ToArray();
        }

        private void OnMessage(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2)
        {
            if (message != MIM_DATA)
                return;
            try
            {
                long packed = param1.ToInt64();
                byte status = (byte)(packed & 0xFF);
                if (status < 0x80 || status >= 0xF0)
                    return; // system messages (clock, active sensing...) aren't used
                int index = (int)instance.ToInt64();
                string device = index >= 0 && index < _names.Count ? _names[index] : "";
                _sink?.Invoke(new MidiMessage(status, (byte)((packed >> 8) & 0x7F), (byte)((packed >> 16) & 0x7F), device));
            }
            catch
            {
                // Never let an exception unwind into winmm's thread.
            }
        }

        public void Dispose()
        {
            foreach (var handle in _handles)
            {
                midiInStop(handle);
                midiInReset(handle);
                midiInClose(handle);
            }
            _handles.Clear();
        }
    }

    /// <summary>
    /// macOS: CoreMIDI. One client and one input port connected to every
    /// source; each source gets its own byte-stream parser (CoreMIDI packets may
    /// carry several messages, with running status).
    /// </summary>
    internal sealed class CoreMidiInput : IMidiBackend
    {
        private const string CoreMidi = "/System/Library/Frameworks/CoreMIDI.framework/CoreMIDI";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const uint kCFStringEncodingUTF8 = 0x08000100;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MidiReadProc(IntPtr packetList, IntPtr readProcRefCon, IntPtr sourceRefCon);

        [DllImport(CoreFoundation)] private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, byte[] cString, uint encoding);
        [DllImport(CoreFoundation)] private static extern bool CFStringGetCString(IntPtr str, byte[] buffer, long bufferSize, uint encoding);
        [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr obj);

        [DllImport(CoreMidi)] private static extern int MIDIClientCreate(IntPtr name, IntPtr notifyProc, IntPtr notifyRefCon, out uint client);
        [DllImport(CoreMidi)] private static extern int MIDIInputPortCreate(uint client, IntPtr portName, MidiReadProc readProc, IntPtr refCon, out uint port);
        [DllImport(CoreMidi)] private static extern nuint MIDIGetNumberOfSources();
        [DllImport(CoreMidi)] private static extern uint MIDIGetSource(nuint index);
        [DllImport(CoreMidi)] private static extern int MIDIPortConnectSource(uint port, uint source, IntPtr connRefCon);
        [DllImport(CoreMidi)] private static extern int MIDIObjectGetStringProperty(uint obj, IntPtr propertyId, out IntPtr str);
        [DllImport(CoreMidi)] private static extern int MIDIClientDispose(uint client);

        private readonly MidiReadProc _readProc;
        private readonly List<MidiStreamParser> _parsers = new();
        private Action<MidiMessage>? _sink;
        private uint _client;

        public CoreMidiInput() => _readProc = OnPackets;

        private static IntPtr CfString(string s) =>
            CFStringCreateWithCString(IntPtr.Zero, Encoding.UTF8.GetBytes(s + "\0"), kCFStringEncodingUTF8);

        public IReadOnlyList<string> Open(Action<MidiMessage> sink)
        {
            _sink = sink;
            var clientName = CfString("DanaProcessing");
            var portName = CfString("DanaProcessing in");
            try
            {
                int status = MIDIClientCreate(clientName, IntPtr.Zero, IntPtr.Zero, out _client);
                if (status != 0)
                    throw new InvalidOperationException($"MIDIClientCreate falló (OSStatus {status}).");
                status = MIDIInputPortCreate(_client, portName, _readProc, IntPtr.Zero, out uint port);
                if (status != 0)
                    throw new InvalidOperationException($"MIDIInputPortCreate falló (OSStatus {status}).");

                IntPtr nameProperty = IntPtr.Zero;
                var lib = NativeLibrary.Load(CoreMidi);
                if (NativeLibrary.TryGetExport(lib, "kMIDIPropertyName", out var symbol))
                    nameProperty = Marshal.ReadIntPtr(symbol);

                // Every parser exists before the first connection: CoreMIDI may
                // call OnPackets for source 0 while we're still connecting the rest.
                var sources = new List<(uint Source, string Name)>();
                nuint count = MIDIGetNumberOfSources();
                for (nuint i = 0; i < count; i++)
                {
                    uint source = MIDIGetSource(i);
                    string name = ReadName(source, nameProperty) ?? $"MIDI {i}";
                    sources.Add((source, name));
                    _parsers.Add(new MidiStreamParser(name));
                }

                var names = new List<string>();
                for (int i = 0; i < sources.Count; i++)
                {
                    // The connection refCon is the parser index, handed back with every packet.
                    if (MIDIPortConnectSource(port, sources[i].Source, (IntPtr)i) == 0)
                        names.Add(sources[i].Name);
                }
                return names;
            }
            finally
            {
                CFRelease(clientName);
                CFRelease(portName);
            }
        }

        private static string? ReadName(uint obj, IntPtr property)
        {
            if (property == IntPtr.Zero || MIDIObjectGetStringProperty(obj, property, out var cf) != 0 || cf == IntPtr.Zero)
                return null;
            try
            {
                var buffer = new byte[256];
                if (!CFStringGetCString(cf, buffer, buffer.Length, kCFStringEncodingUTF8))
                    return null;
                int end = Array.IndexOf(buffer, (byte)0);
                return Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
            }
            finally
            {
                CFRelease(cf);
            }
        }

        private void OnPackets(IntPtr packetList, IntPtr readProcRefCon, IntPtr sourceRefCon)
        {
            try
            {
                int index = (int)sourceRefCon.ToInt64();
                if (index < 0 || index >= _parsers.Count || _sink == null)
                    return;
                var parser = _parsers[index];

                // MIDIPacketList { UInt32 numPackets; MIDIPacket packet[]; } with
                // MIDIPacket { UInt64 timeStamp; UInt16 length; Byte data[]; },
                // packed to 4 bytes. On ARM the next packet starts 4-aligned.
                int packets = Marshal.ReadInt32(packetList);
                IntPtr p = packetList + 4;
                bool arm = RuntimeInformation.ProcessArchitecture is Architecture.Arm64 or Architecture.Arm;
                var data = new byte[256];
                for (int i = 0; i < packets; i++)
                {
                    int length = (ushort)Marshal.ReadInt16(p + 8);
                    length = Math.Min(length, data.Length);
                    Marshal.Copy(p + 10, data, 0, length);
                    parser.Feed(data.AsSpan(0, length), _sink);

                    long next = (p + 10 + length).ToInt64();
                    if (arm)
                        next = (next + 3) & ~3L;
                    p = new IntPtr(next);
                }
            }
            catch
            {
                // Never let an exception unwind into CoreMIDI's thread.
            }
        }

        public void Dispose()
        {
            if (_client != 0)
            {
                MIDIClientDispose(_client); // also disposes the port and its connections
                _client = 0;
            }
        }
    }

    /// <summary>
    /// Linux: ALSA rawmidi, opening device 0 of every sound card that has a MIDI
    /// input ("hw:N,0") -- USB controllers appear as their own card. One reader
    /// thread per device, non-blocking reads.
    /// </summary>
    internal sealed class AlsaRawMidiInput : IMidiBackend
    {
        private const string Alsa = "libasound.so.2";
        private const int SND_RAWMIDI_NONBLOCK = 0x0002;
        private const int EAGAIN = 11;

        [DllImport(Alsa)] private static extern int snd_card_next(ref int card);
        [DllImport(Alsa)] private static extern int snd_card_get_name(int card, out IntPtr name);
        [DllImport(Alsa)] private static extern int snd_rawmidi_open(out IntPtr input, IntPtr output, string name, int mode);
        [DllImport(Alsa)] private static extern unsafe nint snd_rawmidi_read(IntPtr rawmidi, byte* buffer, nuint size);
        [DllImport(Alsa)] private static extern int snd_rawmidi_close(IntPtr rawmidi);

        private readonly List<(IntPtr Handle, Thread Thread)> _open = new();
        private volatile bool _running;

        public IReadOnlyList<string> Open(Action<MidiMessage> sink)
        {
            _running = true;
            var names = new List<string>();
            int card = -1;
            while (snd_card_next(ref card) == 0 && card >= 0)
            {
                if (snd_rawmidi_open(out var handle, IntPtr.Zero, $"hw:{card},0", SND_RAWMIDI_NONBLOCK) < 0)
                    continue; // this card has no MIDI input
                string name = snd_card_get_name(card, out var namePtr) == 0 && namePtr != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(namePtr) ?? $"hw:{card},0"
                    : $"hw:{card},0";
                var parser = new MidiStreamParser(name);
                var thread = new Thread(() => ReadLoop(handle, parser, sink))
                {
                    IsBackground = true,
                    Name = $"DanaProcessing MIDI ({name})",
                };
                _open.Add((handle, thread));
                names.Add(name);
                thread.Start();
            }
            return names;
        }

        private unsafe void ReadLoop(IntPtr handle, MidiStreamParser parser, Action<MidiMessage> sink)
        {
            var buffer = new byte[256];
            while (_running)
            {
                nint read;
                fixed (byte* p = buffer)
                    read = snd_rawmidi_read(handle, p, (nuint)buffer.Length);
                if (read > 0)
                {
                    try { parser.Feed(buffer.AsSpan(0, (int)read), sink); }
                    catch { /* a sink bug must not kill the device thread */ }
                    continue;
                }
                if (read == -EAGAIN || read == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }
                DanaLogger.Warn("MIDI: se desconectó un dispositivo.");
                return;
            }
        }

        public void Dispose()
        {
            _running = false;
            foreach (var (handle, thread) in _open)
            {
                thread.Join(200);
                snd_rawmidi_close(handle);
            }
            _open.Clear();
        }
    }
}
