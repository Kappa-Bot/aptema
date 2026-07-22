using System.Reflection;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace Aptema.App.Presentation;

internal static class NativeWindowPlacement
{
    public static PixelPoint PlaceFlyout(Forms.NotifyIcon notifyIcon, PixelSize size)
    {
        var iconRect = TryGetNotifyIconRect(notifyIcon);
        var workArea = GetWorkArea(iconRect);
        return WindowPlacementPolicy.PlaceFlyout(iconRect, workArea, size);
    }

    public static PixelPoint PlaceOsd(PixelSize size)
    {
        var foreground = GetForegroundWindow();
        var screen = foreground != nint.Zero
            ? Forms.Screen.FromHandle(foreground)
            : Forms.Screen.PrimaryScreen;
        var area = screen?.WorkingArea ?? Forms.SystemInformation.WorkingArea;
        return WindowPlacementPolicy.PlaceOsd(ToPixelRect(area), size);
    }

    private static PixelRect GetWorkArea(PixelRect? iconRect)
    {
        if (iconRect is { } rect)
        {
            var bounds = new System.Drawing.Rectangle(rect.Left, rect.Top, Math.Max(1, rect.Width), Math.Max(1, rect.Height));
            return ToPixelRect(Forms.Screen.FromRectangle(bounds).WorkingArea);
        }

        var area = Forms.Screen.PrimaryScreen?.WorkingArea ?? Forms.SystemInformation.WorkingArea;
        return ToPixelRect(area);
    }

    private static PixelRect? TryGetNotifyIconRect(Forms.NotifyIcon notifyIcon)
    {
        var type = typeof(Forms.NotifyIcon);
        var windowField = FindField(type, "_window", "window");
        var idField = FindField(type, "_id", "id");
        if (windowField?.GetValue(notifyIcon) is not Forms.NativeWindow window ||
            idField?.GetValue(notifyIcon) is not int id ||
            window.Handle == nint.Zero)
        {
            return null;
        }

        var identifier = new NotifyIconIdentifier
        {
            CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            HWnd = window.Handle,
            Id = (uint)id
        };
        return ShellNotifyIconGetRect(ref identifier, out var rect) == 0
            ? new PixelRect(rect.Left, rect.Top, rect.Right, rect.Bottom)
            : null;
    }

    private static FieldInfo? FindField(Type type, params string[] names)
    {
        foreach (var name in names)
        {
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) is { } field)
            {
                return field;
            }
        }

        return null;
    }

    private static PixelRect ToPixelRect(System.Drawing.Rectangle rectangle) =>
        new(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint CbSize;
        public nint HWnd;
        public uint Id;
        public Guid GuidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
    private static extern int ShellNotifyIconGetRect(ref NotifyIconIdentifier identifier, out NativeRect iconLocation);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
