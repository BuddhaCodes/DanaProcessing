using System.Reflection;
using System.Runtime.InteropServices;
using DanaProcessing.AvaloniaHost.Attach;
using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>
/// Every P/Invoke of the attached-sketch host points at a DLL that really
/// exports it. A wrong DLL only fails when the call runs (GetTickCount was
/// declared in user32 instead of kernel32 and the host crashed on the first
/// drag), so this checks them all up front -- the GPU presenter's and UI Automation's too.
/// Windows only; elsewhere it passes.
/// </summary>
public class Win32ImportsTests
{
    [Fact]
    public void Every_import_exists_in_its_dll()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var missing = new List<string>();
        var gpuNative = typeof(DCompWindow).GetNestedType("Native", BindingFlags.NonPublic)!;   // d3d11, dxgi, dcomp
        foreach (var m in typeof(Win32).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                     .Concat(gpuNative.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                     .Concat(typeof(TerminalPanes).GetNestedType("Native", BindingFlags.NonPublic)!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)))
        {
            var import = m.GetCustomAttribute<DllImportAttribute>();
            if (import == null)
                continue;
            string entry = import.EntryPoint ?? m.Name;
            var lib = NativeLibrary.Load(import.Value);
            bool found = NativeLibrary.TryGetExport(lib, entry, out _)
                || (import.CharSet != CharSet.Ansi && NativeLibrary.TryGetExport(lib, entry + "W", out _))
                || NativeLibrary.TryGetExport(lib, entry + "A", out _);
            if (!found)
                missing.Add($"{entry} in {import.Value}");
        }
        Assert.Empty(missing);
    }
}
