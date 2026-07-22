using Aptema.Core;

namespace Aptema.Core.Tests;

public sealed class AppCategoryMapperTests
{
    [Theory]
    [InlineData("chrome.exe", AppCategory.Browser)]
    [InlineData("msedge.exe", AppCategory.Browser)]
    [InlineData("firefox.exe", AppCategory.Browser)]
    [InlineData("outlook.exe", AppCategory.EmailCommunication)]
    [InlineData("teams.exe", AppCategory.EmailCommunication)]
    [InlineData("discord.exe", AppCategory.EmailCommunication)]
    [InlineData("code.exe", AppCategory.Development)]
    [InlineData("devenv.exe", AppCategory.Development)]
    [InlineData("Overwatch.exe", AppCategory.Gaming)]
    [InlineData("FortniteClient-Win64-Shipping.exe", AppCategory.Gaming)]
    [InlineData("dota2.exe", AppCategory.Gaming)]
    [InlineData("bf6.exe", AppCategory.Gaming)]
    [InlineData("vlc.exe", AppCategory.VideoMedia)]
    [InlineData("Spotify.exe", AppCategory.MusicAudio)]
    [InlineData("Ableton Live.exe", AppCategory.MusicAudio)]
    [InlineData("reaper.exe", AppCategory.MusicAudio)]
    [InlineData("Photoshop.exe", AppCategory.Creative)]
    [InlineData("Illustrator.exe", AppCategory.Creative)]
    [InlineData("Figma.exe", AppCategory.Creative)]
    [InlineData("blender.exe", AppCategory.Creative)]
    [InlineData("Resolve.exe", AppCategory.Creative)]
    public void KnownProcessesMapToExpectedCategories(string processName, AppCategory expected)
    {
        var mapper = AppCategoryMapper.CreateDefault();

        Assert.Equal(expected, mapper.Classify(processName));
    }

    [Fact]
    public void UserOverrideWinsOverBuiltInMapping()
    {
        var mapper = AppCategoryMapper.CreateDefault(new Dictionary<string, AppCategory>
        {
            ["chrome.exe"] = AppCategory.Development
        });

        Assert.Equal(AppCategory.Development, mapper.Classify("CHROME.EXE"));
    }

    [Fact]
    public void UnknownProcessFallsBackToUnknown()
    {
        var mapper = AppCategoryMapper.CreateDefault();

        Assert.Equal(AppCategory.Unknown, mapper.Classify("custom-tool.exe"));
    }
}
