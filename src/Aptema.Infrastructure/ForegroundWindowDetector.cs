using System.Diagnostics;
using System.Runtime.InteropServices;
using Aptema.Core;

namespace Aptema.Infrastructure;

public sealed class ForegroundWindowDetector : IForegroundWindowDetector
{
    private readonly AppCategoryMapper _mapper;
    private readonly IFullscreenDetector _fullscreenDetector;

    public ForegroundWindowDetector(AppCategoryMapper? mapper = null, IFullscreenDetector? fullscreenDetector = null)
    {
        _mapper = mapper ?? AppCategoryMapper.CreateDefault();
        _fullscreenDetector = fullscreenDetector ?? new FullscreenDetector();
    }

    public AppContextModel Detect()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero)
        {
            return new AppContextModel(null, AppCategory.Unknown, false);
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        var processName = TryGetProcessName(pid);
        var category = _mapper.Classify(processName);
        var snapshot = BuildWindowSnapshot(hwnd, processName);
        var isFullscreen = snapshot is not null && _fullscreenDetector.IsFullscreen(snapshot);

        return new AppContextModel(processName, category, isFullscreen);
    }

    private static string? TryGetProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? process.ProcessName
                : $"{process.ProcessName}.exe";
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static WindowSnapshot? BuildWindowSnapshot(nint hwnd, string? processName)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var windowRect))
        {
            return null;
        }

        var hMonitor = NativeMethods.MonitorFromWindow(hwnd, 2);
        var info = new NativeMethods.MonitorInfoEx();
        info.cbSize = Marshal.SizeOf<NativeMethods.MonitorInfoEx>();
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
        {
            return null;
        }

        var placement = NativeMethods.WindowPlacement.Create();
        var isMaximized = NativeMethods.GetWindowPlacement(hwnd, ref placement) && placement.showCmd == 3;
        return new WindowSnapshot(
            hwnd,
            processName,
            ToRect(windowRect),
            ToRect(info.rcMonitor),
            ToRect(info.rcWork),
            isMaximized);
    }

    private static RectInt ToRect(NativeMethods.Rect rect)
    {
        return new RectInt(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}
