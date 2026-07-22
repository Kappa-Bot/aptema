using Aptema.Core;
using Aptema.Infrastructure;
using System.Text.Json;

namespace Aptema.Infrastructure.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task LoadAsyncMigratesV4EnvelopeToV6WithoutLosingLearningOrDisplayAliases()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var context = new PreferenceLearningContext("device:\\\\.\\DISPLAY1", AppCategory.Browser, DayPhase.Night, false, LuminanceClassification.Bright);
        var learning = PreferenceLearningService.RecordFeedback(PreferenceLearningModel.Empty, context, ComfortFeedback.TooBright, DateTimeOffset.Parse("2026-07-20T22:00:00Z"));
        var legacySettings = UserSettings.Default with
        {
            SchemaVersion = 3,
            ComfortIntensity = 61,
            PreferenceLearning = learning,
            MonitorPreferences = [new MonitorPreference { MonitorId = "device:\\\\.\\DISPLAY1", BrightnessOffsetPercent = -4 }]
        };
        var v4 = new
        {
            schemaVersion = 4,
            savedAt = DateTimeOffset.Parse("2026-07-20T22:00:00Z"),
            settings = legacySettings
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(v4));

        var first = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);
        var second = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);

        Assert.Equal(6, first.SchemaVersion);
        Assert.Equal(61, first.ComfortIntensity);
        Assert.Single(first.PreferenceLearning.Aggregates);
        var display = Assert.Single(first.DisplayConfigurations);
        Assert.Equal(-4, display.BrightnessOffsetPercent);
        Assert.Contains("device:\\\\.\\DISPLAY1", display.LegacyAliases, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(first.ComfortIntensity, second.ComfortIntensity);
        Assert.Equal(first.PreferenceLearning.Aggregates.Count, second.PreferenceLearning.Aggregates.Count);
        Assert.Equal(first.DisplayConfigurations[0].StableId, second.DisplayConfigurations[0].StableId);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(6, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(4, document.RootElement.GetProperty("migration").GetProperty("sourceSchemaVersion").GetInt32());
    }

    [Fact]
    public async Task SaveAsyncRoundTripsSchema5DisplayAndHotkeyConfiguration()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = UserSettings.Default with
        {
            DisplayConfigurations = [new DisplayConfiguration("display:stable", ["device:\\\\.\\DISPLAY2"], false, 3, 30, 82, true)],
            Hotkeys = new HotkeyConfiguration("Win+Alt+Q", null, null, null, null, null)
        };
        var store = new JsonSettingsStore(path);

        await store.SaveAsync(settings, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        var display = Assert.Single(actual.DisplayConfigurations);
        Assert.Equal("display:stable", display.StableId);
        Assert.Equal(["device:\\\\.\\DISPLAY2"], display.LegacyAliases);
        Assert.False(display.IsEnabled);
        Assert.Equal(settings.Hotkeys, actual.Hotkeys);
    }
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

        Assert.Equal(6, settings.SchemaVersion);
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

        Assert.Equal(6, settings.SchemaVersion);
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

        Assert.Equal(6, settings.SchemaVersion);
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
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(UserSettings.Default with { SchemaVersion = 3, ComfortIntensity = 37 }));
        var store = new JsonSettingsStore(targetPath, legacyPath);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(37, settings.ComfortIntensity);
        Assert.True(File.Exists(legacyPath));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(targetPath));
        Assert.Equal(6, document.RootElement.GetProperty("schemaVersion").GetInt32());
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
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(UserSettings.Default with { SchemaVersion = 3, ComfortIntensity = 37 }));
        var store = new JsonSettingsStore(targetPath, legacyPath);
        await store.LoadAsync(CancellationToken.None);
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(UserSettings.Default with { SchemaVersion = 3, ComfortIntensity = 88 }));

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
        Assert.Equal(6, document.RootElement.GetProperty("schemaVersion").GetInt32());
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
            {"schemaVersion":7,"settings":{"schemaVersion":6,"comfortIntensity":77},"futureField":"keep"}
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
    public async Task SaveAfterLoadingFutureEnvelopeRefusesAndPreservesOriginalFile()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string future = """
            {"schemaVersion":7,"settings":{"schemaVersion":6,"comfortIntensity":77},"futureField":"keep"}
            """;
        await File.WriteAllTextAsync(path, future);
        var store = new JsonSettingsStore(path, legacySettingsPath: null);
        await store.LoadAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<UnsupportedSettingsEnvelopeException>(async () =>
            await store.SaveAsync(UserSettings.Default with { ComfortIntensity = 20 }, CancellationToken.None));

        Assert.Equal(7, exception.SchemaVersion);
        Assert.IsAssignableFrom<IOException>(exception);
        Assert.Equal(future, await File.ReadAllTextAsync(path));
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
        await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(UserSettings.Default with { SchemaVersion = 3, ComfortIntensity = 39 }));
        var store = new JsonSettingsStore(targetPath, legacyPath);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(39, settings.ComfortIntensity);
        Assert.True(File.Exists(legacyPath));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(targetPath));
        var migration = document.RootElement.GetProperty("migration");
        Assert.Equal("LegacyRecovery", migration.GetProperty("kind").GetString());
        Assert.Equal("LightPilot", migration.GetProperty("sourceProduct").GetString());
    }

    [Fact]
    public async Task LoadAsyncMigratesV5EnvelopeToV6WithPersonalizationDefaults()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        await File.WriteAllTextAsync(path, """
            {"schemaVersion":5,"savedAt":"2026-07-22T18:00:00Z","settings":{"schemaVersion":5,"comfortIntensity":52}}
            """);

        var settings = await new JsonSettingsStore(path, legacySettingsPath: null).LoadAsync(CancellationToken.None);

        Assert.Equal(6, settings.SchemaVersion);
        Assert.Equal(52, settings.ComfortIntensity);
        Assert.Empty(settings.ApplicationRules);
        Assert.Empty(settings.CustomProfiles);
        Assert.Empty(settings.AutomationRules);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(6, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(5, document.RootElement.GetProperty("migration").GetProperty("sourceSchemaVersion").GetInt32());
    }

    [Fact]
    public async Task SaveRoundTripsApplicationRulesProfilesAndAutomation()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var expected = UserSettings.Default with
        {
            ApplicationRules = [new("code", "Coding", true, 10, "code.exe", AppCategory.Development, null, "soft-code", -2, true)],
            CustomProfiles = [new("soft-code", "Soft coding", 66, 52, 38, 6000, 4700, 3600, 110, true)],
            AutomationRules = [new("night", "Night softening", true, 10, DayPhase.Night, AppCategory.Development, false, null, -3, -160)]
        };
        var store = new JsonSettingsStore(path, legacySettingsPath: null);

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await new JsonSettingsStore(path, legacySettingsPath: null).LoadAsync(CancellationToken.None);

        Assert.Equal(expected.ApplicationRules, actual.ApplicationRules);
        Assert.Equal(expected.CustomProfiles, actual.CustomProfiles);
        Assert.Equal(expected.AutomationRules, actual.AutomationRules);
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
