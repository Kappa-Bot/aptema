using System.Security.Cryptography;
using System.Text;
using Aptema.Core;

namespace Aptema.Infrastructure;

public sealed record MonitorTopologyCandidate(
    long NativeHandle,
    string DeviceName,
    string? TargetDevicePath,
    string? EdidIdentity,
    string? WmiIdentity,
    string? FriendlyName,
    DisplayBounds Bounds,
    DisplayBounds WorkArea,
    bool IsPrimary);

public sealed record ResolvedMonitorIdentity(string StableId, IReadOnlyList<string> LegacyAliases, string HumanDisplayName);

public static class MonitorIdentityPolicy
{
    public static ResolvedMonitorIdentity Resolve(MonitorTopologyCandidate candidate)
    {
        var source = First(candidate.EdidIdentity, candidate.TargetDevicePath, candidate.WmiIdentity, candidate.DeviceName);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(source))))[..24].ToLowerInvariant();
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(candidate.DeviceName))
        {
            aliases.Add($"device:{candidate.DeviceName.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(candidate.TargetDevicePath))
        {
            aliases.Add($"path:{candidate.TargetDevicePath.Trim()}");
        }

        return new ResolvedMonitorIdentity(
            $"display:{digest}",
            aliases.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            First(candidate.FriendlyName, candidate.DeviceName, "Display"));
    }

    private static string First(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!;

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

public interface IMonitorTopologyProvider
{
    ValueTask<IReadOnlyList<MonitorTopologyCandidate>> GetTopologyAsync(CancellationToken cancellationToken);
}

public interface IMonitorCapabilityProbe
{
    bool SupportsBrightness(long nativeHandle);
}
