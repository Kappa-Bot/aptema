using System.Globalization;

namespace LightPilot.App.Presentation;

public static class ColorContrastPolicy
{
    public static double ContrastRatio(string first, string second)
    {
        var firstLuminance = RelativeLuminance(Parse(first));
        var secondLuminance = RelativeLuminance(Parse(second));
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static (byte Red, byte Green, byte Blue) Parse(string value)
    {
        if (value.Length != 7 || value[0] != '#')
        {
            throw new ArgumentException("Color must use #RRGGBB format.", nameof(value));
        }

        return (
            byte.Parse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static double RelativeLuminance((byte Red, byte Green, byte Blue) color) =>
        0.2126 * Linearize(color.Red) +
        0.7152 * Linearize(color.Green) +
        0.0722 * Linearize(color.Blue);

    private static double Linearize(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
