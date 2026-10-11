using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace DanaProcessing.Audio
{
    /// <summary>
    /// macOS output through AudioToolbox's Audio Queue Services, called
    /// directly via P/Invoke -- part of every macOS, no dependencies. The
    /// queue owns a small ring of buffers and calls us back on its own
    /// thread whenever one has finished playing; we refill it and hand it
    /// straight back.
    ///
    /// Format: 32-bit float, interleaved stereo -- exactly what the engine
    /// renders, so a refill is one render call and one memory copy.
    /// </summary>
    internal sealed class CoreAudioOutput : IAudioOutput
    {
        private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
        private const uint kAudioFormatLinearPCM = 0x6C70636D; // 'lpcm'
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
        private delegate void AudioQueueOutputCallback(IntPtr userData, IntPtr queue, IntPtr buffer);

        [DllImport(AudioToolbox)]
        private static extern int AudioQueueNewOutput(ref AudioStreamBasicDescription format, AudioQueueOutputCallback callback, IntPtr userData,
                                                      IntPtr callbackRunLoop, IntPtr callbackRunLoopMode, uint flags, out IntPtr queue);
        [DllImport(AudioToolbox)] private static extern int AudioQueueAllocateBuffer(IntPtr queue, uint byteSize, out IntPtr buffer);
        [DllImport(AudioToolbox)] private static extern int AudioQueueEnqueueBuffer(IntPtr queue, IntPtr buffer, uint numPacketDescs, IntPtr packetDescs);
        [DllImport(AudioToolbox)] private static extern int AudioQueueStart(IntPtr queue, IntPtr startTime);
        [DllImport(AudioToolbox)] private static extern int AudioQueueStop(IntPtr queue, bool immediate);
        [DllImport(AudioToolbox)] private static extern int AudioQueueDispose(IntPtr queue, bool immediate);

        private readonly int _bufferFrames;
        private readonly int _bufferCount;
        private readonly float[] _scratch;
        private readonly AudioQueueOutputCallback _callback; // kept alive for as long as the queue exists
        private readonly int _byteSizeOffset = (int)Marshal.OffsetOf<AudioQueueBuffer>(nameof(AudioQueueBuffer.mAudioDataByteSize));
        private readonly int _dataOffset = (int)Marshal.OffsetOf<AudioQueueBuffer>(nameof(AudioQueueBuffer.mAudioData));
        private IntPtr _queue;
        private AudioRenderCallback? _render;
        private volatile bool _running;

        public int SampleRate { get; }
        public int LatencyFrames => _bufferFrames * _bufferCount;
        public string Description => $"CoreAudio {SampleRate} Hz, {_bufferCount}x{_bufferFrames * 1000 / SampleRate} ms";

        public CoreAudioOutput(int sampleRate = 48000, int bufferFrames = 512, int bufferCount = 3)
        {
            SampleRate = sampleRate;
            _bufferFrames = bufferFrames;
            _bufferCount = bufferCount;
            _scratch = new float[bufferFrames * 2];
            _callback = OnBufferDone;
        }

        public void Start(AudioRenderCallback render)
        {
            _render = render;
            var format = new AudioStreamBasicDescription
            {
                mSampleRate = SampleRate,
                mFormatID = kAudioFormatLinearPCM,
                mFormatFlags = kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked,
                mBytesPerPacket = 8,
                mFramesPerPacket = 1,
                mBytesPerFrame = 8,
                mChannelsPerFrame = 2,
                mBitsPerChannel = 32,
            };

            // Null run loop = AudioToolbox calls us on its own internal thread.
            int status = AudioQueueNewOutput(ref format, _callback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out _queue);
            if (status != 0)
                throw new InvalidOperationException($"AudioQueueNewOutput falló (OSStatus {status}).");

            _running = true;
            uint bytes = (uint)(_bufferFrames * 8);
            for (int i = 0; i < _bufferCount; i++)
            {
                status = AudioQueueAllocateBuffer(_queue, bytes, out var buffer);
                if (status != 0)
                    throw new InvalidOperationException($"AudioQueueAllocateBuffer falló (OSStatus {status}).");
                Fill(buffer);
                AudioQueueEnqueueBuffer(_queue, buffer, 0, IntPtr.Zero);
            }

            status = AudioQueueStart(_queue, IntPtr.Zero);
            if (status != 0)
                throw new InvalidOperationException($"AudioQueueStart falló (OSStatus {status}).");
        }

        private void OnBufferDone(IntPtr userData, IntPtr queue, IntPtr buffer)
        {
            if (!_running)
                return;
            Fill(buffer);
            AudioQueueEnqueueBuffer(queue, buffer, 0, IntPtr.Zero);
        }

        private void Fill(IntPtr buffer)
        {
            Array.Clear(_scratch);
            _render!(_scratch, _bufferFrames);
            var data = Marshal.ReadIntPtr(buffer, _dataOffset);
            Marshal.Copy(_scratch, 0, data, _scratch.Length);
            Marshal.WriteInt32(buffer, _byteSizeOffset, _scratch.Length * sizeof(float));
        }

        public void Dispose()
        {
            if (_queue == IntPtr.Zero)
                return;
            _running = false;
            AudioQueueStop(_queue, true);
            AudioQueueDispose(_queue, true); // also frees the buffers
            _queue = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Linux output through ALSA (libasound.so.2), called directly via
    /// P/Invoke. The "default" device is used, which on desktop
    /// distributions is routed through PulseAudio or PipeWire, so it plays
    /// along with everything else. A dedicated thread renders a block and
    /// blocks in snd_pcm_writei() until the device takes it -- the device
    /// itself paces us.
    /// </summary>
    internal sealed class AlsaOutput : IAudioOutput
    {
        private const string Alsa = "libasound.so.2";
        private const int SND_PCM_STREAM_PLAYBACK = 0;
        private const int SND_PCM_FORMAT_FLOAT_LE = 14;
        private const int SND_PCM_ACCESS_RW_INTERLEAVED = 3;

        [DllImport(Alsa)] private static extern int snd_pcm_open(out IntPtr pcm, string name, int stream, int mode);
        [DllImport(Alsa)] private static extern int snd_pcm_set_params(IntPtr pcm, int format, int access, uint channels, uint rate, int softResample, uint latencyMicroseconds);
        [DllImport(Alsa)] private static extern unsafe long snd_pcm_writei(IntPtr pcm, float* buffer, ulong frames);
        [DllImport(Alsa)] private static extern int snd_pcm_recover(IntPtr pcm, int err, int silent);
        [DllImport(Alsa)] private static extern int snd_pcm_drop(IntPtr pcm);
        [DllImport(Alsa)] private static extern int snd_pcm_close(IntPtr pcm);
        [DllImport(Alsa)] private static extern IntPtr snd_strerror(int errnum);

        private readonly int _blockFrames;
        private readonly uint _latencyMicros;
        private IntPtr _pcm;
        private Thread? _thread;
        private volatile bool _running;

        public int SampleRate { get; }
        public int LatencyFrames => (int)(_latencyMicros * (long)SampleRate / 1_000_000);
        public string Description => $"ALSA {SampleRate} Hz, {_latencyMicros / 1000} ms";

        public AlsaOutput(int sampleRate = 48000, int blockFrames = 480, uint latencyMicros = 40_000)
        {
            SampleRate = sampleRate;
            _blockFrames = blockFrames;
            _latencyMicros = latencyMicros;
        }

        private static string Error(int code) => Marshal.PtrToStringAnsi(snd_strerror(code)) ?? code.ToString();

        public void Start(AudioRenderCallback render)
        {
            int err = snd_pcm_open(out _pcm, "default", SND_PCM_STREAM_PLAYBACK, 0);
            if (err < 0)
                throw new InvalidOperationException($"snd_pcm_open: {Error(err)}");
            err = snd_pcm_set_params(_pcm, SND_PCM_FORMAT_FLOAT_LE, SND_PCM_ACCESS_RW_INTERLEAVED, 2, (uint)SampleRate, 1, _latencyMicros);
            if (err < 0)
            {
                snd_pcm_close(_pcm);
                _pcm = IntPtr.Zero;
                throw new InvalidOperationException($"snd_pcm_set_params: {Error(err)}");
            }

            _running = true;
            _thread = new Thread(() => Loop(render))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "DanaProcessing audio (ALSA)",
            };
            _thread.Start();
        }

        private unsafe void Loop(AudioRenderCallback render)
        {
            var buffer = new float[_blockFrames * 2];
            while (_running)
            {
                Array.Clear(buffer);
                render(buffer, _blockFrames);
                fixed (float* p = buffer)
                {
                    long offset = 0;
                    while (offset < _blockFrames && _running)
                    {
                        long written = snd_pcm_writei(_pcm, p + offset * 2, (ulong)(_blockFrames - offset));
                        if (written < 0)
                        {
                            // Underrun (-EPIPE) or suspend: recover and keep going.
                            if (snd_pcm_recover(_pcm, (int)written, 1) < 0)
                            {
                                Thread.Sleep(5);
                                break;
                            }
                            continue;
                        }
                        offset += written;
                    }
                }
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
}
