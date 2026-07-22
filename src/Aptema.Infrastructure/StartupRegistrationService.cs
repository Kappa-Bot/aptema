using Microsoft.Win32;

namespace Aptema.Infrastructure;

public sealed class StartupRegistrationService
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "Aptema";
    private const string LegacyValueName = "LightPilot";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string || key?.GetValue(LegacyValueName) is string;
    }

    public void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, BuildStartupCommand(executablePath));
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
    }

    public bool MigrateLegacyRegistration(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (key.GetValue(LegacyValueName) is not string) return false;
        key.SetValue(ValueName, BuildStartupCommand(executablePath));
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        return true;
    }

    public static string BuildStartupCommand(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        if (executablePath.Contains('"'))
        {
            throw new ArgumentException("Executable path cannot contain quotes.", nameof(executablePath));
        }

        return $"\"{executablePath}\" --background";
    }
}
