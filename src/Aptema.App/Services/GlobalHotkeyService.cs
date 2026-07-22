using System.Runtime.InteropServices;
using Aptema.Application;
using Aptema.Core;

namespace Aptema.App.Services;

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

    public OperationResult<HotkeyAction> RegisterDefaults() => RegisterConfigured(HotkeyConfiguration.Default);

    public OperationResult<HotkeyAction> RegisterConfigured(HotkeyConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.QuickAdjust))
        {
            return OperationResult<HotkeyAction>.Failure(OperationStatus.Unavailable, "QuickAdjustHotkeyDisabled");
        }

        if (!TryParse(configuration.QuickAdjust, out var modifiers, out var virtualKey))
        {
            return OperationResult<HotkeyAction>.Failure(OperationStatus.ValidationFailure, "QuickAdjustHotkeyInvalid");
        }

        _registered = registrar.Register(
            QuickAdjustId,
            HotkeyAction.QuickAdjust,
            modifiers | HotkeyModifiers.NoRepeat,
            virtualKey);
        return _registered
            ? OperationResult<HotkeyAction>.Succeeded(HotkeyAction.QuickAdjust)
            : OperationResult<HotkeyAction>.Failure(OperationStatus.Conflict, "QuickAdjustHotkeyUnavailable");
    }

    private static bool TryParse(string gesture, out HotkeyModifiers modifiers, out int virtualKey)
    {
        modifiers = HotkeyModifiers.None;
        virtualKey = 0;
        foreach (var part in gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyModifiers.Windows;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyModifiers.Alt;
            else if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyModifiers.Control;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= HotkeyModifiers.Shift;
            else if (part.Length == 1 && char.IsAsciiLetterOrDigit(part[0]) && virtualKey == 0) virtualKey = char.ToUpperInvariant(part[0]);
            else return false;
        }
        return modifiers != HotkeyModifiers.None && virtualKey != 0;
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
