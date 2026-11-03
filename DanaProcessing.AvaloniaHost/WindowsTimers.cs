using System.Runtime.InteropServices;

namespace DanaProcessing.AvaloniaHost
{
    /// <summary>
    /// A widget is almost never the window in front: you work in other apps
    /// while it animates. Windows 11 treats a process like that as background
    /// work -- EcoQoS ("efficiency mode": slower CPU) and coarse timers -- so
    /// the frame rate sagged until the widget was clicked, then sagged again.
    /// This opts the process out of both, and asks for 1 ms timers (frames at
    /// 60 fps need ~16.7 ms ticks, which the default 15.6 ms resolution
    /// rounds to 31 ms). Counted, so several widgets share it; nothing
    /// anywhere but Windows.
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
