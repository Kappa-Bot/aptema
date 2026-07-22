namespace Aptema.Core;

public static class AppOverrideTextCodec
{
    public static IReadOnlyDictionary<string, AppCategory> Parse(string? text)
    {
        var result = new Dictionary<string, AppCategory>(StringComparer.OrdinalIgnoreCase);
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

            var processName = line[..separator].Trim();
            var categoryText = line[(separator + 1)..].Trim();
            if (processName.Length == 0 || !Enum.TryParse<AppCategory>(categoryText, ignoreCase: true, out var category))
            {
                continue;
            }

            result[processName] = category;
        }

        return result;
    }

    public static string Format(IReadOnlyDictionary<string, AppCategory> overrides)
    {
        return string.Join(
            Environment.NewLine,
            overrides
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={pair.Value}"));
    }
}
