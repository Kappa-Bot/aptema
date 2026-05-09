namespace LightPilot.Core;

public sealed class AppCategoryMapper
{
    private readonly IReadOnlyDictionary<string, AppCategory> _rules;

    private AppCategoryMapper(IReadOnlyDictionary<string, AppCategory> rules)
    {
        _rules = rules;
    }

    public static AppCategoryMapper CreateDefault(IReadOnlyDictionary<string, AppCategory>? overrides = null)
    {
        var rules = new Dictionary<string, AppCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["chrome.exe"] = AppCategory.Browser,
            ["msedge.exe"] = AppCategory.Browser,
            ["firefox.exe"] = AppCategory.Browser,
            ["outlook.exe"] = AppCategory.EmailCommunication,
            ["teams.exe"] = AppCategory.EmailCommunication,
            ["discord.exe"] = AppCategory.EmailCommunication,
            ["code.exe"] = AppCategory.Development,
            ["devenv.exe"] = AppCategory.Development,
            ["steam.exe"] = AppCategory.Gaming,
            ["overwatch.exe"] = AppCategory.Gaming,
            ["fortniteclient-win64-shipping.exe"] = AppCategory.Gaming,
            ["dota2.exe"] = AppCategory.Gaming,
            ["bf6.exe"] = AppCategory.Gaming,
            ["battlefield.exe"] = AppCategory.Gaming,
            ["vlc.exe"] = AppCategory.VideoMedia,
            ["spotify.exe"] = AppCategory.MusicAudio,
            ["ableton live.exe"] = AppCategory.MusicAudio,
            ["reaper.exe"] = AppCategory.MusicAudio,
            ["winword.exe"] = AppCategory.OfficeReading,
            ["excel.exe"] = AppCategory.OfficeReading,
            ["powerpnt.exe"] = AppCategory.OfficeReading,
            ["notepad.exe"] = AppCategory.OfficeReading,
            ["photoshop.exe"] = AppCategory.Creative,
            ["illustrator.exe"] = AppCategory.Creative,
            ["figma.exe"] = AppCategory.Creative,
            ["blender.exe"] = AppCategory.Creative,
            ["resolve.exe"] = AppCategory.Creative
        };

        if (overrides is not null)
        {
            foreach (var (processName, category) in overrides)
            {
                rules[Normalize(processName)] = category;
            }
        }

        return new AppCategoryMapper(rules);
    }

    public AppCategory Classify(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return AppCategory.Unknown;
        }

        var normalized = Normalize(processName);
        return _rules.TryGetValue(normalized, out var category) ? category : AppCategory.Unknown;
    }

    private static string Normalize(string processName)
    {
        var trimmed = processName.Trim();
        var slash = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
    }
}
