using System.Runtime.InteropServices;
using LightPilot.Application;

namespace LightPilot.App.Services;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
    NoRepeat = 0x4000
}

public enum HotkeyAction
{
    QuickAdjust
}

public interface IHotkeyRegistrar
{
    bool Register(int id, HotkeyAction action, HotkeyModifiers modifiers, int virtualKey);
    void Unregister(int id);
}

public sealed class GlobalHotkeyService(IHotkeyRegistrar registrar) : IDisposable
{
    public const int QuickAdjustId = 0xA701;
    private bool _registered;

    public OperationResult<HotkeyAction> RegisterDefaults()
    {
        _registered = registrar.Register(
            QuickAdjustId,
            HotkeyAction.QuickAdjust,
            HotkeyModifiers.Windows | HotkeyModifiers.Alt | HotkeyModifiers.NoRepeat,
            'A');
        return _registered
            ? OperationResult<HotkeyAction>.Succeeded(HotkeyAction.QuickAdjust)
            : OperationResult<HotkeyAction>.Failure(OperationStatus.Conflict, "QuickAdjustHotkeyUnavailable");
    }

    public HotkeyAction? Resolve(int id) => id == QuickAdjustId ? HotkeyAction.QuickAdjust : null;

    public void Dispose()
    {
        if (_registered)
        {
            registrar.Unregister(QuickAdjustId);
            _registered = false;
        }
    }
}

public sealed class Win32HotkeyRegistrar(nint windowHandle) : IHotkeyRegistrar
{
    public bool Register(int id, HotkeyAction action, HotkeyModifiers modifiers, int virtualKey) =>
        RegisterHotKey(windowHandle, id, (uint)modifiers, (uint)virtualKey);

    public void Unregister(int id) => UnregisterHotKey(windowHandle, id);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
