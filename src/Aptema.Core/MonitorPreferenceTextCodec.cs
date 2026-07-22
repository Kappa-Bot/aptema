namespace Aptema.Core;

public static class MonitorPreferenceTextCodec
{
    public static IReadOnlyList<MonitorPreference> Parse(string? text)
    {
        var result = new List<MonitorPreference>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (var rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
            {
                continue;
            }

            var monitorId = line[..separator].Trim();
            var rule = line[(separator + 1)..].Trim();
            if (rule.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new MonitorPreference { MonitorId = monitorId, IsDisabled = true });
                continue;
            }

            if (rule.StartsWith("offset:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(rule["offset:".Length..], out var offset))
            {
                result.Add(new MonitorPreference
                {
                    MonitorId = monitorId,
                    BrightnessOffsetPercent = Math.Clamp(offset, -30, 30)
                });
            }
        }

        return result;
    }

    public static string Format(IEnumerable<MonitorPreference> preferences)
    {
        return string.Join(
            Environment.NewLine,
            preferences
                .Where(item => !string.IsNullOrWhiteSpace(item.MonitorId))
                .OrderBy(item => item.MonitorId, StringComparer.OrdinalIgnoreCase)
                .Select(FormatPreference));
    }

    private static string FormatPreference(MonitorPreference preference)
    {
        if (preference.IsDisabled)
        {
            return $"{preference.MonitorId}=off";
        }

        var sign = preference.BrightnessOffsetPercent >= 0 ? "+" : "";
        return $"{preference.MonitorId}=offset:{sign}{preference.BrightnessOffsetPercent}";
    }
}
