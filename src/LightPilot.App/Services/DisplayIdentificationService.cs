using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Controls;
using LightPilot.App.Presentation;
using LightPilot.Core;

namespace LightPilot.App.Services;

public sealed class DisplayIdentificationService
{
    public void Show(MonitorModel monitor, int number)
    {
        if (!OverlayBoundsPolicy.TryGetPhysicalBounds(monitor.Bounds, out var bounds)) return;
        var window = new Window
        {
            Width = 180, Height = 112, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true, Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 15, 76, 82)),
            ShowInTaskbar = false, Topmost = true, Focusable = false, IsHitTestVisible = false,
            Content = new TextBlock
            {
                Text = number.ToString(), Foreground = System.Windows.Media.Brushes.White, FontSize = 54, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center
            }
        };
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var style = GetWindowLong(hwnd, -20);
            SetWindowLong(hwnd, -20, style | 0x20 | 0x80 | 0x08000000);
            SetWindowPos(hwnd, new nint(-1), bounds.X + ((bounds.Width - 180) / 2), bounds.Y + ((bounds.Height - 112) / 2), 180, 112, 0x0010 | 0x0200);
        };
        window.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { timer.Stop(); window.Close(); };
        timer.Start();
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(nint hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowLong(nint hWnd, int nIndex, int value);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int width, int height, uint flags);
}
