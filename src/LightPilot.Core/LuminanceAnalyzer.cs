namespace LightPilot.Core;

public static class LuminanceAnalyzer
{
    public static ContentLuminanceSample Analyze(ReadOnlySpan<byte> rgbBytes)
    {
        if (rgbBytes.Length < 3)
        {
            return ContentLuminanceSample.Unknown;
        }

        var pixels = rgbBytes.Length / 3;
        var total = 0d;
        var white = 0;
        var bright = 0;
        var dark = 0;
        var saturatedBright = 0;

        for (var i = 0; i < pixels * 3; i += 3)
        {
            var r = rgbBytes[i];
            var g = rgbBytes[i + 1];
            var b = rgbBytes[i + 2];
            var luma = ((0.2126 * r) + (0.7152 * g) + (0.0722 * b)) / 255d;
            var max = Math.Max(r, Math.Max(g, b)) / 255d;
            var min = Math.Min(r, Math.Min(g, b)) / 255d;
            var saturation = max <= 0 ? 0 : (max - min) / max;

            total += luma;
            if (luma <= 0.20)
            {
                dark++;
            }

            if (luma >= 0.72)
            {
                bright++;
            }

            if (max >= 0.88 && saturation > 0.50)
            {
                saturatedBright++;
            }

            if (luma >= 0.88 && saturation <= 0.20)
            {
                white++;
            }
        }

        var average = total / pixels;
        var whiteRatio = (double)white / pixels;
        var brightRatio = (double)(bright + saturatedBright) / pixels;
        var darkRatio = (double)dark / pixels;
        var classification = Classify(average, whiteRatio, brightRatio, darkRatio);

        return new ContentLuminanceSample(true, average, whiteRatio, brightRatio, darkRatio, classification);
    }

    private static LuminanceClassification Classify(double average, double whiteRatio, double brightRatio, double darkRatio)
    {
        if (brightRatio >= 0.35 && darkRatio >= 0.35)
        {
            return LuminanceClassification.HighContrast;
        }

        if (whiteRatio >= 0.60 && average >= 0.72)
        {
            return LuminanceClassification.MostlyWhite;
        }

        if (average >= 0.65 || brightRatio >= 0.50)
        {
            return LuminanceClassification.Bright;
        }

        if (average <= 0.25 && brightRatio <= 0.10)
        {
            return LuminanceClassification.Dark;
        }

        return LuminanceClassification.Balanced;
    }
}
