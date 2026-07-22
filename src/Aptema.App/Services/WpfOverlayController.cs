using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aptema.Core;
using Aptema.Infrastructure;
using Aptema.Application;
using Aptema.App.Presentation;

namespace Aptema.App.Services;

public sealed class WpfOverlayController : IOverlayController, IDisplayTopologyObserver, IDisposable
{
    private readonly Dictionary<string, Window> _windows = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask ApplyAsync(MonitorModel monitor, double opacity, int colorTemperatureKelvin, CancellationToken cancellationToken)
    {
        if (System.Windows.Application.Current is null)
        {
            return ValueTask.CompletedTask;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var window = GetOrCreate(monitor.Id);
            Position(window, monitor.Bounds);
            var targetOpacity = Math.Clamp(opacity, 0, 0.35);
            window.Background = new SolidColorBrush(ToWarmColor(colorTemperatureKelvin, targetOpacity));

            if (targetOpacity > 0.01 && !window.IsVisible)
            {
                window.Opacity = 0;
                window.Show();
                Position(window, monitor.Bounds);
            }

            var fade = new DoubleAnimation(targetOpacity, TimeSpan.FromSeconds(2))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            fade.Completed += (_, _) =>
            {
                if (targetOpacity <= 0.01)
                {
                    window.Hide();
                }
            };

            if (targetOpacity <= 0.01 && !window.IsVisible)
            {
                window.Opacity = 0;
                return;
            }

            window.BeginAnimation(Window.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        });

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateTopologyAsync(IReadOnlyList<MonitorModel> displays, CancellationToken cancellationToken)
    {
        if (System.Windows.Application.Current is null) return ValueTask.CompletedTask;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var current = displays.Select(display => display.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var stale in _windows.Keys.Where(id => !current.Contains(id)).ToArray())
            {
                _windows[stale].Close();
                _windows.Remove(stale);
            }

            foreach (var display in displays)
            {
                if (_windows.TryGetValue(display.Id, out var window)) Position(window, display.Bounds);
            }
        });
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var window in _windows.Values)
        {
            window.Close();
        }

        _windows.Clear();
    }

    private Window GetOrCreate(string monitorId)
    {
        if (_windows.TryGetValue(monitorId, out var existing))
        {
            return existing;
        }

        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            ShowInTaskbar = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            Left = 0,
            Top = 0,
            Width = 1,
            Height = 1,
            IsHitTestVisible = false,
            Focusable = false
        };

        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var style = GetWindowLong(hwnd, GwlExStyle);
            SetWindowLong(hwnd, GwlExStyle, style | WsExTransparent | WsExLayered | WsExToolWindow | WsExNoActivate);
        };

        _windows[monitorId] = window;
        return window;
    }

    private static void Position(Window window, DisplayBounds bounds)
    {
        if (!OverlayBoundsPolicy.TryGetPhysicalBounds(bounds, out var rectangle)) return;
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        SetWindowPos(hwnd, HwndTopmost, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height,
            SwpNoActivate | SwpNoOwnerZOrder);
    }

    private static System.Windows.Media.Color ToWarmColor(int colorTemperatureKelvin, double opacity)
    {
        if (opacity <= 0)
        {
            return Colors.Transparent;
        }

        var warmth = Math.Clamp((6500 - colorTemperatureKelvin) / 3700d, 0, 1);
        return System.Windows.Media.Color.FromArgb(255, 255, (byte)(214 - (24 * warmth)), (byte)(158 - (48 * warmth)));
    }

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private static readonly nint HwndTopmost = new(-1);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
