using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace DanaProcessing.Audio
{
    /// <summary>Fills <paramref name="interleavedStereo"/> with <paramref name="frames"/> frames (L, R, L, R...).</summary>
    internal delegate void AudioRenderCallback(float[] interleavedStereo, int frames);

    /// <summary>
    /// The only part of the audio module that talks to the operating system:
    /// "here's a callback, keep the sound card fed". Everything musical
    /// (synths, scheduling, effects, analysis) lives above this, in our own
    /// code. Adding macOS/Linux later means adding one more implementation
    /// of this interface, nothing else.
    /// </summary>
    internal interface IAudioOutput : IDisposable
    {
        int SampleRate { get; }

        /// <summary>Roughly how far behind the engine clock the speakers are, in frames.</summary>
        int LatencyFrames { get; }

        /// <summary>Short human-readable name for diagnostics ("WinMM 48000 Hz, 4x10 ms").</summary>
        string Description { get; }

        void Start(AudioRenderCallback render);
    }

    /// <summary>
    /// Windows output through the classic waveOut API (winmm.dll), called
    /// directly via P/Invoke -- zero dependencies, available on every
    /// Windows since forever. A dedicated high-priority thread keeps a small
    /// ring of buffers queued; the driver signals an event each time one
    /// finishes playing and we refill it.
    ///
    /// Latency is BufferCount x BufferFrames (40 ms by default). Live loops
    /// are scheduled 100 ms ahead anyway, so this doesn't affect their
    /// timing, only how fast an interactive Play() (e.g. on a key press) is
    /// heard. A WASAPI backend can replace this later for lower latency.
    /// </summary>
    internal sealed class WinMmOutput : IAudioOutput
    {
        private const int WAVE_MAPPER = -1;
        private const int CALLBACK_EVENT = 0x00050000;
        private const int WHDR_DONE = 0x00000001;
        private const int MMSYSERR_NOERROR = 0;

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

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WaveFormatEx lpFormat, IntPtr dwCallback, IntPtr dwInstance, int dwFlags);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr hWaveOut);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr hWaveOut);

        private readonly int _bufferFrames;
        private readonly int _bufferCount;
        private readonly int _headerSize = Marshal.SizeOf<WaveHdr>();
        private readonly int _flagsOffset = (int)Marshal.OffsetOf<WaveHdr>(nameof(WaveHdr.dwFlags));

        private IntPtr _device;
        private IntPtr[] _headers = Array.Empty<IntPtr>();
        private IntPtr[] _data = Array.Empty<IntPtr>();
        private readonly AutoResetEvent _bufferDone = new(false);
        private Thread? _thread;
        private volatile bool _running;
        private AudioRenderCallback? _render;

        public int SampleRate { get; }
        public int LatencyFrames => _bufferFrames * _bufferCount;
        public string Description => $"WinMM {SampleRate} Hz, {_bufferCount}x{_bufferFrames * 1000 / SampleRate} ms";

        public WinMmOutput(int sampleRate = 48000, int bufferFrames = 480, int bufferCount = 4)
        {
            SampleRate = sampleRate;
            _bufferFrames = bufferFrames;
            _bufferCount = bufferCount;
        }

        public void Start(AudioRenderCallback render)
        {
            _render = render;

            var format = new WaveFormatEx
            {
                wFormatTag = 1, // PCM
                nChannels = 2,
                nSamplesPerSec = (uint)SampleRate,
                wBitsPerSample = 16,
                nBlockAlign = 4,
                nAvgBytesPerSec = (uint)(SampleRate * 4),
                cbSize = 0,
            };

            int result = waveOutOpen(out _device, WAVE_MAPPER, ref format, _bufferDone.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
            if (result != MMSYSERR_NOERROR)
                throw new InvalidOperationException($"waveOutOpen falló (código {result}).");

            int bytes = _bufferFrames * 4;
            _headers = new IntPtr[_bufferCount];
            _data = new IntPtr[_bufferCount];
            for (int i = 0; i < _bufferCount; i++)
            {
                _data[i] = Marshal.AllocHGlobal(bytes);
                _headers[i] = Marshal.AllocHGlobal(_headerSize);
                var header = new WaveHdr { lpData = _data[i], dwBufferLength = (uint)bytes };
                Marshal.StructureToPtr(header, _headers[i], false);
                waveOutPrepareHeader(_device, _headers[i], _headerSize);
            }

            _running = true;
            _thread = new Thread(RunLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "DanaProcessing audio (WinMM)",
            };
            _thread.Start();
        }

        private void RunLoop()
        {
            var floats = new float[_bufferFrames * 2];
            var shorts = new short[_bufferFrames * 2];

            void Fill(int index)
            {
                Array.Clear(floats);
                _render!(floats, _bufferFrames);
                for (int s = 0; s < floats.Length; s++)
                {
                    float v = floats[s];
                    v = v > 1f ? 1f : v < -1f ? -1f : v;
                    shorts[s] = (short)(v * 32767f);
                }
                Marshal.Copy(shorts, 0, _data[index], shorts.Length);
                waveOutWrite(_device, _headers[index], _headerSize);
            }

            for (int i = 0; i < _bufferCount && _running; i++)
                Fill(i);

            int next = 0;
            while (_running)
            {
                _bufferDone.WaitOne(50);
                while (_running && (Marshal.ReadInt32(_headers[next], _flagsOffset) & WHDR_DONE) != 0)
                {
                    Fill(next);
                    next = (next + 1) % _bufferCount;
                }
            }
        }

        public void Dispose()
        {
            if (!_running && _device == IntPtr.Zero)
                return;

            _running = false;
            _bufferDone.Set();
            _thread?.Join(500);

            if (_device != IntPtr.Zero)
            {
                waveOutReset(_device);
                for (int i = 0; i < _headers.Length; i++)
                {
                    waveOutUnprepareHeader(_device, _headers[i], _headerSize);
                    Marshal.FreeHGlobal(_headers[i]);
                    Marshal.FreeHGlobal(_data[i]);
                }
                waveOutClose(_device);
                _device = IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// No sound card: renders and discards audio in real time so the engine
    /// clock (and therefore every live loop) keeps ticking exactly as if a
    /// device were there. Used on platforms that don't have an output
    /// backend yet, or when opening the real device fails.
    /// </summary>
    internal sealed class ClockOnlyOutput : IAudioOutput
    {
        private readonly int _blockFrames;
        private Thread? _thread;
        private volatile bool _running;

        public int SampleRate { get; }
        public int LatencyFrames => 0;
        public string Description => $"sin salida de audio (solo reloj) {SampleRate} Hz";

        public ClockOnlyOutput(int sampleRate = 48000, int blockFrames = 480)
        {
            SampleRate = sampleRate;
            _blockFrames = blockFrames;
        }

        public void Start(AudioRenderCallback render)
        {
            _running = true;
            _thread = new Thread(() =>
            {
                var buffer = new float[_blockFrames * 2];
                var clock = Stopwatch.StartNew();
                long rendered = 0;
                while (_running)
                {
                    long due = (long)(clock.Elapsed.TotalSeconds * SampleRate);
                    while (rendered + _blockFrames <= due)
                    {
                        Array.Clear(buffer);
                        render(buffer, _blockFrames);
                        rendered += _blockFrames;
                    }
                    Thread.Sleep(2);
                }
            })
            {
                IsBackground = true,
                Name = "DanaProcessing audio (clock only)",
            };
            _thread.Start();
        }

        public void Dispose()
        {
            _running = false;
            _thread?.Join(200);
        }
    }
}
