using System.IO;

namespace LightPilot.App.Presentation;

public static class ApplicationDisplayNamePolicy
{
    public static string GetDisplayName(string? processName)
    {
        var name = Path.GetFileNameWithoutExtension(processName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name) || name.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "Current application";
        }

        if (name.Equals("LightPilot.App", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Aptema", StringComparison.OrdinalIgnoreCase))
        {
            return "Aptema";
        }

        return name.Equals("code", StringComparison.OrdinalIgnoreCase) ? "Visual Studio Code" : name;
    }
}
