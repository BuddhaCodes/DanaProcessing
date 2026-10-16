using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace DanaProcessing.Audio
{
    /// <summary>Receives <paramref name="count"/> mono samples (-1..1) from the capture thread. The buffer is reused: copy what you keep.</summary>
    internal delegate void AudioCaptureCallback(float[] mono, int count);

    /// <summary>
    /// The microphone side of <see cref="IAudioOutput"/>: "here's a callback,
    /// keep it fed with what the default input hears". Mono, 48 kHz, one
    /// implementation per OS -- WinMM, CoreAudio (Audio Queue) and ALSA -- all
    /// plain P/Invoke into what the OS already has.
    /// </summary>
    internal interface IAudioInput : IDisposable
    {
        int SampleRate { get; }
        string Description { get; }
        void Start(AudioCaptureCallback onSamples);
    }

    internal static class AudioInputs
    {
        public static IAudioInput? CreateDefault()
        {
            if (OperatingSystem.IsWindows())
                return new WinMmInput();
            if (OperatingSystem.IsMacOS())
                return new CoreAudioInput();
            if (OperatingSystem.IsLinux())
                return new AlsaInput();
            return null;
        }
    }

    /// <summary>Windows: waveIn (winmm.dll) on the default recording device, 16-bit mono.</summary>
    internal sealed class WinMmInput : IAudioInput
    {
        private const int WAVE_MAPPER = -1;
        private const int CALLBACK_EVENT = 0x00050000;
        private const int WHDR_DONE = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormatEx
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHdr
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")] private static extern int waveInOpen(out IntPtr handle, int deviceId, ref WaveFormatEx format, IntPtr callback, IntPtr instance, int flags);
        [DllImport("winmm.dll")] private static extern int waveInPrepareHeader(IntPtr handle, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInUnprepareHeader(IntPtr handle, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInAddBuffer(IntPtr handle, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInStart(IntPtr handle);
        [DllImport("winmm.dll")] private static extern int waveInReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern int waveInClose(IntPtr handle);

        private const int BufferFrames = 480;   // 10 ms
        private const int BufferCount = 4;

        private readonly int _headerSize = Marshal.SizeOf<WaveHdr>();
        private readonly int _flagsOffset = (int)Marshal.OffsetOf<WaveHdr>(nameof(WaveHdr.dwFlags));
        private readonly int _recordedOffset = (int)Marshal.OffsetOf<WaveHdr>(nameof(WaveHdr.dwBytesRecorded));
        private readonly AutoResetEvent _bufferDone = new(false);
        private IntPtr _device;
        private IntPtr[] _headers = Array.Empty<IntPtr>();
        private IntPtr[] _data = Array.Empty<IntPtr>();
        private Thread? _thread;
        private volatile bool _running;

        public int SampleRate => 48000;
        public string Description => "WinMM (micrófono predeterminado)";

        public void Start(AudioCaptureCallback onSamples)
        {
            var format = new WaveFormatEx
            {
                wFormatTag = 1,
                nChannels = 1,
                nSamplesPerSec = (uint)SampleRate,
                wBitsPerSample = 16,
                nBlockAlign = 2,
                nAvgBytesPerSec = (uint)(SampleRate * 2),
            };
            int result = waveInOpen(out _device, WAVE_MAPPER, ref format, _bufferDone.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
            if (result != 0)
                throw new InvalidOperationException($"waveInOpen falló (código {result}) -- ¿hay un micrófono conectado y permitido?");

            _headers = new IntPtr[BufferCount];
            _data = new IntPtr[BufferCount];
            for (int i = 0; i < BufferCount; i++)
            {
                _data[i] = Marshal.AllocHGlobal(BufferFrames * 2);
                _headers[i] = Marshal.AllocHGlobal(_headerSize);
                Marshal.StructureToPtr(new WaveHdr { lpData = _data[i], dwBufferLength = BufferFrames * 2 }, _headers[i], false);
                waveInPrepareHeader(_device, _headers[i], _headerSize);
                waveInAddBuffer(_device, _headers[i], _headerSize);
            }

            _running = true;
            _thread = new Thread(() => Loop(onSamples)) { IsBackground = true, Name = "DanaProcessing mic (WinMM)" };
            _thread.Start();
            waveInStart(_device);
        }

        private void Loop(AudioCaptureCallback onSamples)
        {
            var shorts = new short[BufferFrames];
            var floats = new float[BufferFrames];
            int next = 0;
            while (_running)
            {
                _bufferDone.WaitOne(50);
                while (_running && (Marshal.ReadInt32(_headers[next], _flagsOffset) & WHDR_DONE) != 0)
                {
                    int bytes = Marshal.ReadInt32(_headers[next], _recordedOffset);
                    int count = Math.Min(BufferFrames, bytes / 2);
                    Marshal.Copy(_data[next], shorts, 0, count);
                    for (int i = 0; i < count; i++)
                        floats[i] = shorts[i] / 32768f;
                    onSamples(floats, count);

                    // Hand the buffer back to the driver.
                    Marshal.WriteInt32(_headers[next], _flagsOffset, Marshal.ReadInt32(_headers[next], _flagsOffset) & ~WHDR_DONE);
                    waveInAddBuffer(_device, _headers[next], _headerSize);
                    next = (next + 1) % BufferCount;
                }
            }
        }

        public void Dispose()
        {
            _running = false;
            _bufferDone.Set();
            _thread?.Join(500);
            if (_device == IntPtr.Zero)
                return;
            waveInReset(_device);
            for (int i = 0; i < _headers.Length; i++)
            {
                waveInUnprepareHeader(_device, _headers[i], _headerSize);
                Marshal.FreeHGlobal(_headers[i]);
                Marshal.FreeHGlobal(_data[i]);
            }
            waveInClose(_device);
            _device = IntPtr.Zero;
        }
    }

    /// <summary>
    /// macOS: an input Audio Queue (AudioToolbox) on the default input, 32-bit
    /// float mono. macOS asks the user for microphone permission the first
    /// time; until it's granted the queue delivers silence.
    /// </summary>
    internal sealed class CoreAudioInput : IAudioInput
    {
        private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
        private const uint kAudioFormatLinearPCM = 0x6C70636D;
        private const uint kAudioFormatFlagIsFloat = 1;
        private const uint kAudioFormatFlagIsPacked = 8;

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioStreamBasicDescription
        {
            public double mSampleRate;
            public uint mFormatID;
            public uint mFormatFlags;
            public uint mBytesPerPacket;
            public uint mFramesPerPacket;
            public uint mBytesPerFrame;
            public uint mChannelsPerFrame;
            public uint mBitsPerChannel;
            public uint mReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioQueueBuffer
        {
            public uint mAudioDataBytesCapacity;
            public IntPtr mAudioData;
            public uint mAudioDataByteSize;
            public IntPtr mUserData;
            public uint mPacketDescriptionCapacity;
            public IntPtr mPacketDescriptions;
            public uint mPacketDescriptionCount;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AudioQueueInputCallback(IntPtr userData, IntPtr queue, IntPtr buffer, IntPtr startTime, uint packetDescs, IntPtr packetDescriptions);

        [DllImport(AudioToolbox)]
        private static extern int AudioQueueNewInput(ref AudioStreamBasicDescription format, AudioQueueInputCallback callback, IntPtr userData,
                                                     IntPtr runLoop, IntPtr runLoopMode, uint flags, out IntPtr queue);
        [DllImport(AudioToolbox)] private static extern int AudioQueueAllocateBuffer(IntPtr queue, uint byteSize, out IntPtr buffer);
        [DllImport(AudioToolbox)] private static extern int AudioQueueEnqueueBuffer(IntPtr queue, IntPtr buffer, uint numPacketDescs, IntPtr packetDescs);
        [DllImport(AudioToolbox)] private static extern int AudioQueueStart(IntPtr queue, IntPtr startTime);
        [DllImport(AudioToolbox)] private static extern int AudioQueueStop(IntPtr queue, bool immediate);
        [DllImport(AudioToolbox)] private static extern int AudioQueueDispose(IntPtr queue, bool immediate);

        private const int BufferFrames = 512;
        private const int BufferCount = 3;

        private readonly AudioQueueInputCallback _callback; // kept alive as long as the queue exists
        private readonly int _dataOffset = (int)Marshal.OffsetOf<AudioQueueBuffer>(nameof(AudioQueueBuffer.mAudioData));
        private readonly int _sizeOffset = (int)Marshal.OffsetOf<AudioQueueBuffer>(nameof(AudioQueueBuffer.mAudioDataByteSize));
        private readonly float[] _scratch = new float[BufferFrames];
        private AudioCaptureCallback? _onSamples;
        private IntPtr _queue;
        private volatile bool _running;

        public CoreAudioInput() => _callback = OnBuffer;

        public int SampleRate => 48000;
        public string Description => "CoreAudio (micrófono predeterminado)";

        public void Start(AudioCaptureCallback onSamples)
        {
            _onSamples = onSamples;
            var format = new AudioStreamBasicDescription
            {
                mSampleRate = SampleRate,
                mFormatID = kAudioFormatLinearPCM,
                mFormatFlags = kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked,
                mBytesPerPacket = 4,
                mFramesPerPacket = 1,
                mBytesPerFrame = 4,
                mChannelsPerFrame = 1,
                mBitsPerChannel = 32,
            };
            int status = AudioQueueNewInput(ref format, _callback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out _queue);
            if (status != 0)
                throw new InvalidOperationException($"AudioQueueNewInput falló (OSStatus {status}).");

            _running = true;
            for (int i = 0; i < BufferCount; i++)
            {
                if (AudioQueueAllocateBuffer(_queue, BufferFrames * 4, out var buffer) != 0)
                    throw new InvalidOperationException("AudioQueueAllocateBuffer falló.");
                AudioQueueEnqueueBuffer(_queue, buffer, 0, IntPtr.Zero);
            }
            status = AudioQueueStart(_queue, IntPtr.Zero);
            if (status != 0)
                throw new InvalidOperationException($"AudioQueueStart falló (OSStatus {status}) -- ¿permiso de micrófono?");
        }

        private void OnBuffer(IntPtr userData, IntPtr queue, IntPtr buffer, IntPtr startTime, uint packetDescs, IntPtr packetDescriptions)
        {
            if (!_running)
                return;
            try
            {
                int bytes = Marshal.ReadInt32(buffer, _sizeOffset);
                int count = Math.Min(_scratch.Length, bytes / 4);
                Marshal.Copy(Marshal.ReadIntPtr(buffer, _dataOffset), _scratch, 0, count);
                _onSamples?.Invoke(_scratch, count);
            }
            catch
            {
                // Never let an exception unwind into AudioToolbox's thread.
            }
            AudioQueueEnqueueBuffer(queue, buffer, 0, IntPtr.Zero);
        }

        public void Dispose()
        {
            if (_queue == IntPtr.Zero)
                return;
            _running = false;
            AudioQueueStop(_queue, true);
            AudioQueueDispose(_queue, true);
            _queue = IntPtr.Zero;
        }
    }

    /// <summary>Linux: ALSA capture on "default" (PulseAudio/PipeWire route it to the selected microphone).</summary>
    internal sealed class AlsaInput : IAudioInput
    {
        private const string Alsa = "libasound.so.2";
        private const int SND_PCM_STREAM_CAPTURE = 1;
        private const int SND_PCM_FORMAT_FLOAT_LE = 14;
        private const int SND_PCM_ACCESS_RW_INTERLEAVED = 3;

        [DllImport(Alsa)] private static extern int snd_pcm_open(out IntPtr pcm, string name, int stream, int mode);
        [DllImport(Alsa)] private static extern int snd_pcm_set_params(IntPtr pcm, int format, int access, uint channels, uint rate, int softResample, uint latencyMicroseconds);
        [DllImport(Alsa)] private static extern unsafe long snd_pcm_readi(IntPtr pcm, float* buffer, ulong frames);
        [DllImport(Alsa)] private static extern int snd_pcm_recover(IntPtr pcm, int err, int silent);
        [DllImport(Alsa)] private static extern int snd_pcm_drop(IntPtr pcm);
        [DllImport(Alsa)] private static extern int snd_pcm_close(IntPtr pcm);

        private const int BlockFrames = 480;
        private IntPtr _pcm;
        private Thread? _thread;
        private volatile bool _running;

        public int SampleRate => 48000;
        public string Description => "ALSA (micrófono predeterminado)";

        public void Start(AudioCaptureCallback onSamples)
        {
            if (snd_pcm_open(out _pcm, "default", SND_PCM_STREAM_CAPTURE, 0) < 0)
                throw new InvalidOperationException("snd_pcm_open (captura) falló -- ¿hay un micrófono?");
            if (snd_pcm_set_params(_pcm, SND_PCM_FORMAT_FLOAT_LE, SND_PCM_ACCESS_RW_INTERLEAVED, 1, (uint)SampleRate, 1, 50_000) < 0)
            {
                snd_pcm_close(_pcm);
                _pcm = IntPtr.Zero;
                throw new InvalidOperationException("snd_pcm_set_params (captura) falló.");
            }
            _running = true;
            _thread = new Thread(() => Loop(onSamples)) { IsBackground = true, Name = "DanaProcessing mic (ALSA)" };
            _thread.Start();
        }

        private unsafe void Loop(AudioCaptureCallback onSamples)
        {
            var buffer = new float[BlockFrames];
            while (_running)
            {
                long read;
                fixed (float* p = buffer)
                    read = snd_pcm_readi(_pcm, p, BlockFrames);
                if (read > 0)
                {
                    onSamples(buffer, (int)read);
                    continue;
                }
                if (read < 0 && snd_pcm_recover(_pcm, (int)read, 1) < 0)
                    Thread.Sleep(5);
            }
        }

        public void Dispose()
        {
            _running = false;
            _thread?.Join(500);
            if (_pcm != IntPtr.Zero)
            {
                snd_pcm_drop(_pcm);
                snd_pcm_close(_pcm);
                _pcm = IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// The one microphone every sketch shares. Opened when the first sketch
    /// starts listening (ListenTo(AudioSource.Microphone), MicLevel) and closed
    /// again when the last one stops -- so the OS's "microphone in use" light
    /// is only on while a sketch actually listens.
    /// </summary>
    internal static class Microphone
    {
        private static readonly object Gate = new();
        private static IAudioInput? _input;
        private static int _listeners;
        private static string _status = "";

        /// <summary>Tests swap in a fake input here.</summary>
        internal static Func<IAudioInput?> Factory { get; set; } = AudioInputs.CreateDefault;

        public static AudioAnalyzer Analyzer { get; } = new();

        /// <summary>Multiplies what the microphone hears before analysis (1 = as is).</summary>
        public static float Gain { get; set; } = 1f;

        public static bool IsOpen
        {
            get { lock (Gate) return _input != null; }
        }

        public static string Status
        {
            get { lock (Gate) return _status; }
        }

        public static int Listeners
        {
            get { lock (Gate) return _listeners; }
        }

        public static void Acquire()
        {
            lock (Gate)
            {
                _listeners++;
                if (_input != null || _listeners > 1)
                    return;

                IAudioInput? input = null;
                try
                {
                    input = Factory();
                    if (input == null)
                    {
                        _status = "No hay entrada de micrófono en este sistema operativo.";
                        AudioEngine.Report(_status);
                        return;
                    }
                    Analyzer.Reset(); // nothing stale from a previous session
                    Analyzer.SampleRate = input.SampleRate;
                    input.Start(OnSamples);
                    _input = input;
                    _status = input.Description;
                    DanaLogger.Info("Micrófono: " + input.Description);
                }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or BadImageFormatException)
                {
                    try { input?.Dispose(); } catch { }
                    _status = $"No se pudo abrir el micrófono ({ex.Message}).";
                    AudioEngine.Report(_status);
                }
            }
        }

        public static void Release()
        {
            IAudioInput? toClose = null;
            lock (Gate)
            {
                if (_listeners == 0)
                    return;
                _listeners--;
                if (_listeners == 0)
                {
                    toClose = _input;
                    _input = null;
                }
            }
            try { toClose?.Dispose(); } catch (Exception ex) { DanaLogger.ErrorFromException(ex, "Cerrando el micrófono"); }
        }

        [ThreadStatic] private static float[]? _gained;

        private static void OnSamples(float[] mono, int count)
        {
            float gain = Gain;
            if (gain == 1f)
            {
                Analyzer.Write(mono.AsSpan(0, count));
                return;
            }
            var scaled = _gained ??= new float[4096];
            if (scaled.Length < count)
                scaled = _gained = new float[count];
            for (int i = 0; i < count; i++)
                scaled[i] = Math.Clamp(mono[i] * gain, -1f, 1f);
            Analyzer.Write(scaled.AsSpan(0, count));
        }
    }
}
