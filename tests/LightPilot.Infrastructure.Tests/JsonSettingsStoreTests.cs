using LightPilot.Core;
using LightPilot.Infrastructure;
using System.Text.Json;

namespace LightPilot.Infrastructure.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task LoadAsyncReturnsDefaultsWhenSettingsFileIsMissing()
    {
        using var temp = new TempDirectory();
        var store = new JsonSettingsStore(Path.Combine(temp.Path, "settings.json"));

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.True(settings.AutoEnabled);
        Assert.False(settings.EnableContentBrightnessAnalysis);
        Assert.Equal(UserSettings.Default.MinimumBrightnessPercent, settings.MinimumBrightnessPercent);
        Assert.Equal(UserSettings.Default.MaximumBrightnessPercent, settings.MaximumBrightnessPercent);
    }

    [Fact]
    public async Task SaveAsyncRoundTripsSettings()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var store = new JsonSettingsStore(path);
        var expected = UserSettings.Default with
        {
            ComfortIntensity = 42,
            EnableDdcCi = false,
            EnableContentBrightnessAnalysis = true,
            MinimumBrightnessPercent = 30
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(42, actual.ComfortIntensity);
        Assert.False(actual.EnableDdcCi);
        Assert.True(actual.EnableContentBrightnessAnalysis);
        Assert.Equal(30, actual.MinimumBrightnessPercent);
    }

    [Fact]
    public async Task LoadAsyncMigratesV1DefaultComfortToGentlerDefaults()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var legacy = new UserSettings
        {
            SchemaVersion = 1,
            ComfortIntensity = 50,
            TransitionSpeed = TimeSpan.FromSeconds(45)
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(legacy));
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(3, settings.SchemaVersion);
        Assert.Equal(45, settings.ComfortIntensity);
        Assert.Equal(TimeSpan.FromSeconds(90), settings.TransitionSpeed);
    }

    [Fact]
    public async Task LoadAsyncPreservesV1CustomComfortDuringMigration()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var legacy = new UserSettings
        {
            SchemaVersion = 1,
            ComfortIntensity = 72,
            TransitionSpeed = TimeSpan.FromSeconds(130)
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(legacy));
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(3, settings.SchemaVersion);
        Assert.Equal(72, settings.ComfortIntensity);
        Assert.Equal(TimeSpan.FromSeconds(130), settings.TransitionSpeed);
    }

    [Fact]
    public async Task LoadAsyncMigratesV2SettingsToPreferenceLearningDefaults()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var legacy = new UserSettings
        {
            SchemaVersion = 2,
            ComfortIntensity = 47
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(legacy));
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(3, settings.SchemaVersion);
        Assert.True(settings.EnablePreferenceLearning);
        Assert.Empty(settings.PreferenceLearning.Aggregates);
        Assert.Equal(47, settings.ComfortIntensity);
    }

    [Fact]
    public async Task SaveAsyncRoundTripsLearnedPreferences()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var store = new JsonSettingsStore(path);
        var context = new PreferenceLearningContext("m1", AppCategory.Browser, DayPhase.Night, false, LuminanceClassification.MostlyWhite);
        var learning = PreferenceLearningService.RecordFeedback(PreferenceLearningModel.Empty, context, ComfortFeedback.TooBright, DateTimeOffset.UtcNow);
        var expected = UserSettings.Default with { PreferenceLearning = learning };

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        var adjustment = PreferenceLearningService.GetAdjustment(actual.PreferenceLearning, context);
        Assert.True(adjustment.IsLearned);
        Assert.Equal(-2, adjustment.BrightnessOffsetPercent);
    }

    [Fact]
    public async Task CorruptFileIsQuarantinedAndDefaultsReturned()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        await File.WriteAllTextAsync(path, "{not-json");
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.True(settings.AutoEnabled);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.EnumerateFiles(temp.Path, "settings.json.corrupt-*"));
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "light-pilot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
