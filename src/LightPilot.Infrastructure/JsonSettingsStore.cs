using System.Text.Json;
using LightPilot.Application;
using LightPilot.Core;

namespace LightPilot.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    public const int CurrentEnvelopeSchemaVersion = 6;
    private const int BackupCount = 3;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly string? _legacySettingsPath;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int? _unsupportedEnvelopeSchemaVersion;

    public JsonSettingsStore()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aptema", "config", "settings.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightPilot", "settings.json"))
    {
    }

    public JsonSettingsStore(string settingsPath)
        : this(settingsPath, legacySettingsPath: null)
    {
    }

    public JsonSettingsStore(string settingsPath, string? legacySettingsPath, IClock? clock = null)
    {
        _settingsPath = settingsPath;
        _legacySettingsPath = legacySettingsPath;
        _clock = clock ?? new TimeProviderClock();
    }

    public async ValueTask<UserSettings> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_settingsPath))
            {
                var current = await TryReadSettingsAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
                if (current?.Kind == SettingsDocumentKind.UnsupportedEnvelope)
                {
                    _unsupportedEnvelopeSchemaVersion = current.DocumentSchemaVersion;
                    return UserSettings.Default;
                }

                if (current is { Settings: not null } && current.Kind is SettingsDocumentKind.Envelope or SettingsDocumentKind.PreviousEnvelope or SettingsDocumentKind.FlatLegacy)
                {
                    _unsupportedEnvelopeSchemaVersion = null;
                    var normalized = Normalize(current.Settings);
                    if (current.Kind is SettingsDocumentKind.FlatLegacy or SettingsDocumentKind.PreviousEnvelope)
                    {
                        await SaveEnvelopeAsync(normalized, MigrationMetadata.Upgrade(current.DocumentSchemaVersion ?? current.Settings.SchemaVersion, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
                    }

                    return normalized;
                }

                QuarantineCorruptFile(_settingsPath);
                var recovered = await TryRecoverBackupAsync(cancellationToken).ConfigureAwait(false);
                if (recovered is not null)
                {
                    await SaveEnvelopeAsync(recovered, MigrationMetadata.Recovery(_clock.UtcNow), cancellationToken).ConfigureAwait(false);
                    return recovered;
                }

                var legacyRecovery = await TryImportLegacyAsync(isRecovery: true, cancellationToken).ConfigureAwait(false);
                if (legacyRecovery is not null)
                {
                    return legacyRecovery;
                }

                return UserSettings.Default;
            }

            var imported = await TryImportLegacyAsync(isRecovery: false, cancellationToken).ConfigureAwait(false);
            return imported ?? UserSettings.Default;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SaveAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_unsupportedEnvelopeSchemaVersion is null && File.Exists(_settingsPath))
            {
                var current = await TryReadSettingsAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
                if (current?.Kind == SettingsDocumentKind.UnsupportedEnvelope)
                {
                    _unsupportedEnvelopeSchemaVersion = current.DocumentSchemaVersion;
                }
            }

            if (_unsupportedEnvelopeSchemaVersion is { } schemaVersion)
            {
                throw new UnsupportedSettingsEnvelopeException(schemaVersion);
            }

            await SaveEnvelopeAsync(Normalize(settings), migration: null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<UserSettings?> TryImportLegacyAsync(bool isRecovery, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_legacySettingsPath) || !File.Exists(_legacySettingsPath))
        {
            return null;
        }

        var legacy = await TryReadSettingsAsync(_legacySettingsPath, cancellationToken).ConfigureAwait(false);
        if (legacy is not { Settings: not null } || legacy.Kind is not (SettingsDocumentKind.Envelope or SettingsDocumentKind.PreviousEnvelope or SettingsDocumentKind.FlatLegacy))
        {
            return null;
        }

        var settings = Normalize(legacy.Settings);
        var migration = MigrationMetadata.Legacy(legacy.Settings.SchemaVersion, _clock.UtcNow, isRecovery);
        await SaveEnvelopeAsync(settings, migration, cancellationToken).ConfigureAwait(false);
        return settings;
    }

    private async ValueTask<UserSettings?> TryRecoverBackupAsync(CancellationToken cancellationToken)
    {
        for (var index = 1; index <= BackupCount; index++)
        {
            var backup = await TryReadSettingsAsync(BackupPath(index), cancellationToken).ConfigureAwait(false);
            if (backup is { Settings: not null } && backup.Kind is SettingsDocumentKind.Envelope or SettingsDocumentKind.PreviousEnvelope or SettingsDocumentKind.FlatLegacy)
            {
                return Normalize(backup.Settings);
            }
        }

        return null;
    }

    private static async ValueTask<SettingsReadResult?> TryReadSettingsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(root, "schemaVersion", out var schemaElement) ||
                schemaElement.ValueKind != JsonValueKind.Number ||
                !schemaElement.TryGetInt32(out var schemaVersion))
            {
                return SettingsReadResult.Malformed;
            }

            if (schemaVersion > CurrentEnvelopeSchemaVersion)
            {
                return new SettingsReadResult(SettingsDocumentKind.UnsupportedEnvelope, null, schemaVersion);
            }

            if (schemaVersion == CurrentEnvelopeSchemaVersion)
            {
                if (!TryGetProperty(root, "settings", out var settingsElement) || settingsElement.ValueKind != JsonValueKind.Object)
                {
                    return SettingsReadResult.Malformed;
                }

                var settings = settingsElement.Deserialize<UserSettings>(Options);
                return settings is null
                    ? SettingsReadResult.Malformed
                    : new SettingsReadResult(SettingsDocumentKind.Envelope, settings, schemaVersion);
            }

            if (schemaVersion is 4 or 5)
            {
                if (!TryGetProperty(root, "settings", out var settingsElement) || settingsElement.ValueKind != JsonValueKind.Object)
                {
                    return SettingsReadResult.Malformed;
                }

                var settings = settingsElement.Deserialize<UserSettings>(Options);
                return settings is null
                    ? SettingsReadResult.Malformed
                    : new SettingsReadResult(SettingsDocumentKind.PreviousEnvelope, settings, schemaVersion);
            }

            if (schemaVersion is >= 1 and <= 3 && !TryGetProperty(root, "settings", out _))
            {
                var legacy = root.Deserialize<UserSettings>(Options);
                return legacy is null
                    ? SettingsReadResult.Malformed
                    : new SettingsReadResult(SettingsDocumentKind.FlatLegacy, legacy, schemaVersion);
            }

            return SettingsReadResult.Malformed;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or IOException)
        {
            return SettingsReadResult.Malformed;
        }
    }

    private async ValueTask SaveEnvelopeAsync(UserSettings settings, MigrationMetadata? migration, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var envelope = new SettingsEnvelope(
            CurrentEnvelopeSchemaVersion,
            _clock.UtcNow,
            settings,
            migration);
        var tempPath = $"{_settingsPath}.tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, envelope, Options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(_settingsPath))
            {
                RotateBackups();
                File.Replace(tempPath, _settingsPath, BackupPath(1), ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _settingsPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private void RotateBackups()
    {
        for (var index = BackupCount; index >= 2; index--)
        {
            var source = BackupPath(index - 1);
            if (File.Exists(source))
            {
                File.Move(source, BackupPath(index), overwrite: true);
            }
        }

        if (File.Exists(BackupPath(1)))
        {
            File.Delete(BackupPath(1));
        }
    }

    private UserSettings Normalize(UserSettings? settings)
    {
        if (settings is null)
        {
            return UserSettings.Default;
        }

        var intensity = Math.Clamp(settings.ComfortIntensity, 0, 100);
        var transitionSpeed = settings.TransitionSpeed;
        if (settings.SchemaVersion < 2)
        {
            if (intensity == 50)
            {
                intensity = UserSettings.Default.ComfortIntensity;
            }

            if (transitionSpeed == TimeSpan.FromSeconds(45) || transitionSpeed == TimeSpan.Zero)
            {
                transitionSpeed = UserSettings.Default.TransitionSpeed;
            }
        }

        transitionSpeed = TimeSpan.FromSeconds(Math.Clamp(transitionSpeed.TotalSeconds, 30, 240));
        var minimum = Math.Clamp(settings.MinimumBrightnessPercent, 15, 100);
        var maximum = Math.Clamp(settings.MaximumBrightnessPercent, minimum, 100);
        return settings with
        {
            SchemaVersion = UserSettings.Default.SchemaVersion,
            ComfortIntensity = intensity,
            MinimumBrightnessPercent = minimum,
            MaximumBrightnessPercent = maximum,
            TransitionSpeed = transitionSpeed,
            EnablePreferenceLearning = settings.SchemaVersion < 3 || settings.EnablePreferenceLearning,
            PreferenceLearning = settings.PreferenceLearning ?? PreferenceLearningModel.Empty,
            DisplayConfigurations = NormalizeDisplays(settings),
            Hotkeys = settings.Hotkeys ?? HotkeyConfiguration.Default,
            ApplicationRules = NormalizeApplicationRules(settings.ApplicationRules),
            CustomProfiles = NormalizeCustomProfiles(settings.CustomProfiles),
            AutomationRules = NormalizeAutomationRules(settings.AutomationRules)
        };
    }

    private static IReadOnlyList<ApplicationComfortRule> NormalizeApplicationRules(IReadOnlyList<ApplicationComfortRule>? rules) =>
        (rules ?? Array.Empty<ApplicationComfortRule>())
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Id) && !string.IsNullOrWhiteSpace(rule.ProcessName))
            .Select(rule => rule with
            {
                Id = rule.Id.Trim(),
                Name = string.IsNullOrWhiteSpace(rule.Name) ? rule.ProcessName.Trim() : rule.Name.Trim(),
                ProcessName = Path.GetFileName(rule.ProcessName.Trim()),
                Priority = Math.Clamp(rule.Priority, -100, 100),
                IntensityOffset = Math.Clamp(rule.IntensityOffset, -20, 20)
            })
            .GroupBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .Take(200)
            .ToArray();

    private static IReadOnlyList<CustomComfortProfile> NormalizeCustomProfiles(IReadOnlyList<CustomComfortProfile>? profiles) =>
        (profiles ?? Array.Empty<CustomComfortProfile>())
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Id) && !string.IsNullOrWhiteSpace(profile.Name))
            .Select(profile => profile with
            {
                Id = profile.Id.Trim(),
                Name = profile.Name.Trim(),
                DayBrightness = Math.Clamp(profile.DayBrightness, 15, 100),
                EveningBrightness = Math.Clamp(profile.EveningBrightness, 15, 100),
                NightBrightness = Math.Clamp(profile.NightBrightness, 15, 100),
                DayKelvin = Math.Clamp(profile.DayKelvin, 2800, 6500),
                EveningKelvin = Math.Clamp(profile.EveningKelvin, 2800, 6500),
                NightKelvin = Math.Clamp(profile.NightKelvin, 2800, 6500),
                TransitionSeconds = Math.Clamp(profile.TransitionSeconds, 30, 240)
            })
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToArray();

    private static IReadOnlyList<ComfortAutomationRule> NormalizeAutomationRules(IReadOnlyList<ComfortAutomationRule>? rules) =>
        (rules ?? Array.Empty<ComfortAutomationRule>())
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Id))
            .Select(rule => rule with
            {
                Id = rule.Id.Trim(),
                Name = string.IsNullOrWhiteSpace(rule.Name) ? "Comfort rule" : rule.Name.Trim(),
                Priority = Math.Clamp(rule.Priority, -100, 100),
                BrightnessOffsetPercent = Math.Clamp(rule.BrightnessOffsetPercent, -12, 12),
                WarmthOffsetKelvin = Math.Clamp(rule.WarmthOffsetKelvin, -480, 480)
            })
            .GroupBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .Take(200)
            .ToArray();

    private static IReadOnlyList<DisplayConfiguration> NormalizeDisplays(UserSettings settings)
    {
        if (settings.DisplayConfigurations is { Count: > 0 })
        {
            return settings.DisplayConfigurations.Select(item => item with
            {
                LegacyAliases = item.LegacyAliases ?? Array.Empty<string>(),
                BrightnessOffsetPercent = Math.Clamp(item.BrightnessOffsetPercent, -20, 20),
                MinimumBrightnessPercent = Math.Clamp(item.MinimumBrightnessPercent, 15, 100),
                MaximumBrightnessPercent = Math.Clamp(item.MaximumBrightnessPercent, Math.Clamp(item.MinimumBrightnessPercent, 15, 100), 100)
            }).ToArray();
        }

        return settings.MonitorPreferences.Select(item => new DisplayConfiguration(
            StableId: StableLegacyId(item.MonitorId),
            LegacyAliases: [item.MonitorId],
            IsEnabled: !item.IsDisabled,
            BrightnessOffsetPercent: Math.Clamp(item.BrightnessOffsetPercent, -20, 20),
            MinimumBrightnessPercent: item.MinimumBrightnessPercent ?? settings.MinimumBrightnessPercent,
            MaximumBrightnessPercent: item.MaximumBrightnessPercent ?? settings.MaximumBrightnessPercent,
            AllowSoftwareFallback: item.UseSoftwareFallback)).ToArray();
    }

    private static string StableLegacyId(string legacyId)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacyId.Trim().ToUpperInvariant()));
        return $"display:{Convert.ToHexString(hash)[..24].ToLowerInvariant()}";
    }

    private void QuarantineCorruptFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var corruptPath = $"{path}.corrupt-{_clock.UtcNow:yyyyMMddHHmmssfff}";
        File.Move(path, corruptPath, overwrite: true);
    }

    private string BackupPath(int index) => $"{_settingsPath}.lkg.{index}";

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private enum SettingsDocumentKind
    {
        Envelope,
        PreviousEnvelope,
        FlatLegacy,
        UnsupportedEnvelope,
        Malformed
    }

    private sealed record SettingsReadResult(SettingsDocumentKind Kind, UserSettings? Settings, int? DocumentSchemaVersion)
    {
        public static SettingsReadResult Malformed { get; } = new(SettingsDocumentKind.Malformed, null, null);
    }
}

public sealed class UnsupportedSettingsEnvelopeException(int schemaVersion)
    : IOException($"Aptema settings schema {schemaVersion} is newer than supported schema {JsonSettingsStore.CurrentEnvelopeSchemaVersion}.")
{
    public int SchemaVersion { get; } = schemaVersion;
}

public sealed record SettingsEnvelope(
    int SchemaVersion,
    DateTimeOffset SavedAt,
    UserSettings Settings,
    MigrationMetadata? Migration);

public sealed record MigrationMetadata(
    string Kind,
    string? SourceProduct,
    int? SourceSchemaVersion,
    DateTimeOffset MigratedAt)
{
    public static MigrationMetadata Legacy(int sourceSchemaVersion, DateTimeOffset migratedAt, bool isRecovery) =>
        new(isRecovery ? "LegacyRecovery" : "LegacyImport", "LightPilot", sourceSchemaVersion, migratedAt);

    public static MigrationMetadata Upgrade(int sourceSchemaVersion, DateTimeOffset migratedAt) =>
        new("EnvelopeUpgrade", null, sourceSchemaVersion, migratedAt);

    public static MigrationMetadata Recovery(DateTimeOffset migratedAt) =>
        new("BackupRecovery", null, SourceSchemaVersion: null, migratedAt);
}
