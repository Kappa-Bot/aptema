using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class AppOverrideTextCodecTests
{
    [Fact]
    public void ParseReadsProcessCategoryPairs()
    {
        var overrides = AppOverrideTextCodec.Parse("""
            chrome.exe=Development
            vlc.exe=VideoMedia
            """);

        Assert.Equal(AppCategory.Development, overrides["chrome.exe"]);
        Assert.Equal(AppCategory.VideoMedia, overrides["vlc.exe"]);
    }

    [Fact]
    public void ParseIgnoresInvalidLines()
    {
        var overrides = AppOverrideTextCodec.Parse("""
            bad-line
            app.exe=NotARealCategory
            code.exe=Development
            """);

        Assert.Single(overrides);
        Assert.Equal(AppCategory.Development, overrides["code.exe"]);
    }

    [Fact]
    public void FormatWritesStableSortedLines()
    {
        var text = AppOverrideTextCodec.Format(new Dictionary<string, AppCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["vlc.exe"] = AppCategory.VideoMedia,
            ["chrome.exe"] = AppCategory.Development
        });

        Assert.Equal("chrome.exe=Development" + Environment.NewLine + "vlc.exe=VideoMedia", text);
    }
}
