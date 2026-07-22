using System.Text.Json;

namespace Aptema.Infrastructure;

public sealed record StartupHealthResult(bool ShouldStartSafeMode, int ConsecutiveFailures, string ReasonCode);

public sealed class StartupHealthGuard
{
    private const int FailureThreshold = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _path;

    public StartupHealthGuard(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aptema", "state", "startup-health.json");
    }

    public StartupHealthResult BeginStartup()
    {
        var recovered = false;
        var state = Read(ref recovered);
        var failures = state.StartupPending ? Math.Min(state.ConsecutiveFailures + 1, FailureThreshold) : state.ConsecutiveFailures;
        var safeMode = recovered || state.SafeModeRequested || failures >= FailureThreshold;
        Write(new StartupHealthState(failures, true, safeMode));
        return new StartupHealthResult(safeMode, failures, recovered ? "StartupStateRecovered" : safeMode ? "RepeatedStartupFailure" : "NormalStartup");
    }

    public void MarkHealthy() => Write(new StartupHealthState(0, false, false));

    public void RequestSafeMode() => Write(new StartupHealthState(FailureThreshold, false, true));

    public void ClearSafeMode() => MarkHealthy();

    private StartupHealthState Read(ref bool recovered)
    {
        if (!File.Exists(_path)) return new StartupHealthState(0, false, false);
        try
        {
            return JsonSerializer.Deserialize<StartupHealthState>(File.ReadAllText(_path), JsonOptions)
                ?? new StartupHealthState(0, false, false);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            recovered = true;
            return new StartupHealthState(FailureThreshold, false, true);
        }
    }

    private void Write(StartupHealthState state)
    {
        var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Startup health path has no directory.");
        Directory.CreateDirectory(directory);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temp, _path, overwrite: true);
    }

    private sealed record StartupHealthState(int ConsecutiveFailures, bool StartupPending, bool SafeModeRequested);
}
