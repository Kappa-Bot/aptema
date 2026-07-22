using System.Text.Json;
using LightPilot.Application;
using LightPilot.Core;

namespace LightPilot.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    public const int CurrentEnvelopeSchemaVersion = 4;
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
                    return UserSettings.Default;
                }

                if (current is { Settings: not null } && current.Kind is SettingsDocumentKind.Envelope or SettingsDocumentKind.FlatLegacy)
                {
                    var normalized = Normalize(current.Settings);
                    if (current.Kind == SettingsDocumentKind.FlatLegacy)
                    {
                        await SaveEnvelopeAsync(normalized, MigrationMetadata.Upgrade(current.Settings.SchemaVersion, _clock.UtcNow), cancellationToken).ConfigureAwait(false);
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
        if (legacy is not { Settings: not null } || legacy.Kind is not (SettingsDocumentKind.Envelope or SettingsDocumentKind.FlatLegacy))
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
            if (backup is { Settings: not null } && backup.Kind is SettingsDocumentKind.Envelope or SettingsDocumentKind.FlatLegacy)
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
                return SettingsReadResult.Unsupported;
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
                    : new SettingsReadResult(SettingsDocumentKind.Envelope, settings);
            }

            if (schemaVersion is >= 1 and <= 3 && !TryGetProperty(root, "settings", out _))
            {
                var legacy = root.Deserialize<UserSettings>(Options);
                return legacy is null
                    ? SettingsReadResult.Malformed
                    : new SettingsReadResult(SettingsDocumentKind.FlatLegacy, legacy);
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
            PreferenceLearning = settings.PreferenceLearning ?? PreferenceLearningModel.Empty
        };
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
        FlatLegacy,
        UnsupportedEnvelope,
        Malformed
    }

    private sealed record SettingsReadResult(SettingsDocumentKind Kind, UserSettings? Settings)
    {
        public static SettingsReadResult Unsupported { get; } = new(SettingsDocumentKind.UnsupportedEnvelope, null);
        public static SettingsReadResult Malformed { get; } = new(SettingsDocumentKind.Malformed, null);
    }
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
