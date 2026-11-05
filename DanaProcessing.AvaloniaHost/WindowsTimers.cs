using System.Runtime.InteropServices;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// Keeps a running sketch window at full speed on Windows. Frames are
    /// paced by a timer, and Windows' default timer resolution is 15.6 ms:
    /// 60 fps (16.7 ms) rounds to 31 ms ticks -- about 32 fps, uneven. It
    /// looked fine only while another app (a video, music) raised the
    /// resolution for the whole system. This asks for 1 ms timers, and opts
    /// the process out of power throttling: a widget is almost never the
    /// window in front, and Windows 11 treats such a process as background
    /// work (EcoQoS: slower CPU, coarse timers). Counted, so several windows
    /// share it; nothing anywhere but Windows.
    /// </summary>
    internal static class WindowsTimers
    {
        private static int s_users;
        private static readonly object s_lock = new();

        /// <summary>True if it took effect (so call Release() later).</summary>
        public static bool KeepFullSpeed()
        {
            if (!OperatingSystem.IsWindows())
                return false;
            lock (s_lock)
            {
                if (s_users++ == 0)
                {
                    try
                    {
                        var state = new PowerThrottlingState
                        {
                            Version = 1,
                            ControlMask = ExecutionSpeed | IgnoreTimerResolution,
                            StateMask = 0, // controlled, and off: never throttle
                        };
                        SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf<PowerThrottlingState>());
                    }
                    catch { /* before Windows 10 1709: nothing to opt out of */ }
                    try { timeBeginPeriod(1); } catch { }
                }
                return true;
            }
        }

        public static void Release()
        {
            if (!OperatingSystem.IsWindows())
                return;
            lock (s_lock)
            {
                if (s_users == 0 || --s_users > 0)
                    return;
                try { timeEndPeriod(1); } catch { }
            }
        }

        private const int ProcessPowerThrottling = 4;          // PROCESS_INFORMATION_CLASS.ProcessPowerThrottling
        private const uint ExecutionSpeed = 0x1;               // PROCESS_POWER_THROTTLING_EXECUTION_SPEED (EcoQoS)
        private const uint IgnoreTimerResolution = 0x4;        // PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION (Windows 11)

        [StructLayout(LayoutKind.Sequential)]
        private struct PowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(IntPtr process, int informationClass, ref PowerThrottlingState information, uint size);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint ms);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint ms);
    }
}
