using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LightPilot.App.Presentation;
using LightPilot.Application;

namespace LightPilot.App;

public partial class OsdWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly OsdPresentationController _presentation;
    private Action? _undo;

    public OsdWindow() : this(new TimeProviderClock())
    {
    }

    internal OsdWindow(IClock clock)
    {
        InitializeComponent();
        _presentation = new OsdPresentationController(clock);
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(handle, GwlExStyle, GetWindowLongPtr(handle, GwlExStyle) | WsExNoActivate | WsExToolWindow);
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            _presentation.Refresh();
            if (!_presentation.Current.IsVisible)
            {
                Hide();
            }
        };
    }

    public void ShowFeedback(string message, bool canUndo, Action undo)
    {
        MessageText.Text = message;
        _presentation.Show(message, canUndo);
        UndoButton.Visibility = canUndo ? Visibility.Visible : Visibility.Collapsed;
        _undo = canUndo ? undo : null;
        Opacity = 0;
        Show();
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;
        var point = NativeWindowPlacement.PlaceOsd(new PixelSize(
            (int)Math.Ceiling(width * dpi.DpiScaleX),
            (int)Math.Ceiling(height * dpi.DpiScaleY)));
        Left = point.X / dpi.DpiScaleX;
        Top = point.Y / dpi.DpiScaleY;
        Opacity = 1;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _undo?.Invoke();
        Hide();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
