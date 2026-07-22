using System.Windows.Interop;
using Aptema.Application;
using Aptema.Core;

namespace Aptema.App.Services;

public sealed class HotkeyHost : IDisposable
{
    private const int WmHotkey = 0x0312;
    private readonly HwndSource _source;
    private readonly GlobalHotkeyService _service;

    public HotkeyHost(HotkeyConfiguration? configuration = null)
    {
        _source = new HwndSource(new HwndSourceParameters("Aptema.Hotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        });
        _source.AddHook(WndProc);
        _service = new GlobalHotkeyService(new Win32HotkeyRegistrar(_source.Handle));
        Registration = _service.RegisterConfigured(configuration ?? HotkeyConfiguration.Default);
    }

    public event Action<HotkeyAction>? Invoked;
    public OperationResult<HotkeyAction> Registration { get; }

    public void Dispose()
    {
        _service.Dispose();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && _service.Resolve(wParam.ToInt32()) is { } action)
        {
            handled = true;
            Invoked?.Invoke(action);
        }

        return nint.Zero;
    }
}
