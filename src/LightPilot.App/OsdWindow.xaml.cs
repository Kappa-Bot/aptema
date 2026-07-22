using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LightPilot.App.Presentation;
using LightPilot.Application;
using Forms = System.Windows.Forms;

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
        var cursor = Forms.Cursor.Position;
        var area = Forms.Screen.FromPoint(cursor).WorkingArea;
        Show();
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = area.Right / dpi.DpiScaleX - ActualWidth - 18;
        Top = area.Top / dpi.DpiScaleY + 18;
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
