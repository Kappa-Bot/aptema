using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class LuminanceAnalyzerTests
{
    [Fact]
    public void AllWhiteFrameIsMostlyWhite()
    {
        var sample = LuminanceAnalyzer.Analyze(CreateRgb(255, 255, 255, 100));

        Assert.Equal(LuminanceClassification.MostlyWhite, sample.Classification);
        Assert.True(sample.WhitePixelRatio > 0.9);
    }

    [Fact]
    public void AllBlackFrameIsDark()
    {
        var sample = LuminanceAnalyzer.Analyze(CreateRgb(0, 0, 0, 100));

        Assert.Equal(LuminanceClassification.Dark, sample.Classification);
        Assert.True(sample.DarkPixelRatio > 0.9);
    }

    [Fact]
    public void SaturatedBrightFrameIsBrightNotMostlyWhite()
    {
        var sample = LuminanceAnalyzer.Analyze(CreateRgb(255, 40, 40, 100));

        Assert.Equal(LuminanceClassification.Bright, sample.Classification);
        Assert.True(sample.WhitePixelRatio < 0.1);
    }

    [Fact]
    public void GrayFrameIsBalanced()
    {
        var sample = LuminanceAnalyzer.Analyze(CreateRgb(120, 120, 120, 100));

        Assert.Equal(LuminanceClassification.Balanced, sample.Classification);
    }

    [Fact]
    public void BrightAndDarkFrameIsHighContrast()
    {
        var bytes = new List<byte>();
        bytes.AddRange(CreateRgb(255, 255, 255, 50));
        bytes.AddRange(CreateRgb(0, 0, 0, 50));

        var sample = LuminanceAnalyzer.Analyze(bytes.ToArray());

        Assert.Equal(LuminanceClassification.HighContrast, sample.Classification);
        Assert.True(sample.BrightPixelRatio >= 0.5);
        Assert.True(sample.DarkPixelRatio >= 0.5);
    }

    private static byte[] CreateRgb(byte r, byte g, byte b, int pixels)
    {
        var bytes = new byte[pixels * 3];
        for (var i = 0; i < bytes.Length; i += 3)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
        }

        return bytes;
    }
}
