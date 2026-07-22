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

    [Fact]
    public async Task LoadAsyncImportsLegacyV3IntoV4EnvelopeWithoutDeletingLegacy()
    {
        using var temp = new TempDirectory();
        var targetPath = Path.Combine(temp.Path, "Aptema", "config", "settings.json");
        var legacyPath = Path.Combine(temp.Path, "LightPilot", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(UserSettings.Default with { ComfortIntensity = 37 }));
        var store = new JsonSettingsStore(targetPath, legacyPath);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(37, settings.ComfortIntensity);
        Assert.True(File.Exists(legacyPath));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(targetPath));
        Assert.Equal(4, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(37, document.RootElement.GetProperty("settings").GetProperty("comfortIntensity").GetInt32());
        Assert.Equal("LightPilot", document.RootElement.GetProperty("migration").GetProperty("sourceProduct").GetString());
    }

    [Fact]
    public async Task LegacyImportIsIdempotentOnceAptemaSettingsExist()
    {
        using var temp = new TempDirectory();
        var targetPath = Path.Combine(temp.Path, "Aptema", "config", "settings.json");
        var legacyPath = Path.Combine(temp.Path, "LightPilot", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(UserSettings.Default with { ComfortIntensity = 37 }));
        var store = new JsonSettingsStore(targetPath, legacyPath);
        await store.LoadAsync(CancellationToken.None);
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(UserSettings.Default with { ComfortIntensity = 88 }));

        var settings = await new JsonSettingsStore(targetPath, legacyPath).LoadAsync(CancellationToken.None);

        Assert.Equal(37, settings.ComfortIntensity);
        Assert.True(File.Exists(legacyPath));
    }

    [Fact]
    public async Task CorruptCurrentSettingsRecoverFromLastKnownGoodBackup()
    {
        using var temp = new TempDirectory();
        var targetPath = Path.Combine(temp.Path, "Aptema", "config", "settings.json");
        var store = new JsonSettingsStore(targetPath, legacySettingsPath: null);
        await store.SaveAsync(UserSettings.Default with { ComfortIntensity = 31 }, CancellationToken.None);
        await store.SaveAsync(UserSettings.Default with { ComfortIntensity = 62 }, CancellationToken.None);
        await File.WriteAllTextAsync(targetPath, "{broken");

        var recovered = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(31, recovered.ComfortIntensity);
        Assert.True(File.Exists(targetPath));
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(targetPath)!, "settings.json.corrupt-*"));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(targetPath));
        Assert.Equal(4, document.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task SaveAsyncRotatesThreeLastKnownGoodBackupsAndLeavesNoTemporaryFile()
    {
        using var temp = new TempDirectory();
        var targetPath = Path.Combine(temp.Path, "settings.json");
        var store = new JsonSettingsStore(targetPath, legacySettingsPath: null);

        foreach (var intensity in new[] { 10, 20, 30, 40, 50 })
        {
            await store.SaveAsync(UserSettings.Default with { ComfortIntensity = intensity }, CancellationToken.None);
        }

        Assert.True(File.Exists($"{targetPath}.lkg.1"));
        Assert.True(File.Exists($"{targetPath}.lkg.2"));
        Assert.True(File.Exists($"{targetPath}.lkg.3"));
        Assert.False(File.Exists($"{targetPath}.tmp"));
    }

    [Fact]
    public async Task FutureEnvelopeIsPreservedAndNotRewritten()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string future = """
            {"schemaVersion":5,"settings":{"schemaVersion":3,"comfortIntensity":77},"futureField":"keep"}
            """;
        await File.WriteAllTextAsync(path, future);
        var store = new JsonSettingsStore(path, legacySettingsPath: null);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(UserSettings.Default.ComfortIntensity, settings.ComfortIntensity);
        Assert.Equal(UserSettings.Default.AutoEnabled, settings.AutoEnabled);
        Assert.Equal(future, await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "settings.json.corrupt-*"));
    }

    [Fact]
    public async Task V4EnvelopeWithoutSettingsIsQuarantined()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":4,\"savedAt\":\"2026-07-22T18:00:00Z\"}");
        var store = new JsonSettingsStore(path, legacySettingsPath: null);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(UserSettings.Default.ComfortIntensity, settings.ComfortIntensity);
        Assert.Equal(UserSettings.Default.AutoEnabled, settings.AutoEnabled);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.EnumerateFiles(temp.Path, "settings.json.corrupt-*"));
    }

    [Fact]
    public async Task CorruptAptemaConfigAndBackupRecoverFromLegacyWithProvenance()
    {
        using var temp = new TempDirectory();
        var targetPath = Path.Combine(temp.Path, "Aptema", "config", "settings.json");
        var legacyPath = Path.Combine(temp.Path, "LightPilot", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        await File.WriteAllTextAsync(targetPath, "{broken");
        await File.WriteAllTextAsync($"{targetPath}.lkg.1", "{also-broken");
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(UserSettings.Default with { ComfortIntensity = 39 }));
        var store = new JsonSettingsStore(targetPath, legacyPath);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(39, settings.ComfortIntensity);
        Assert.True(File.Exists(legacyPath));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(targetPath));
        var migration = document.RootElement.GetProperty("migration");
        Assert.Equal("LegacyRecovery", migration.GetProperty("kind").GetString());
        Assert.Equal("LightPilot", migration.GetProperty("sourceProduct").GetString());
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
