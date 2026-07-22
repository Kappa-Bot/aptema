using System.Runtime.InteropServices;
using LightPilot.Core;

namespace LightPilot.Infrastructure;

public sealed class MonitorEnumerator : IMonitorEnumerator
{
    private const uint McCapsBrightness = 0x00000002;
    private readonly IMonitorTopologyProvider _topologyProvider;
    private readonly IMonitorCapabilityProbe _capabilityProbe;

    public MonitorEnumerator() : this(new WindowsMonitorTopologyProvider(), new DdcCapabilityProbe())
    {
    }

    public MonitorEnumerator(IMonitorTopologyProvider topologyProvider, IMonitorCapabilityProbe capabilityProbe)
    {
        _topologyProvider = topologyProvider;
        _capabilityProbe = capabilityProbe;
    }

    public async ValueTask<IReadOnlyList<MonitorModel>> EnumerateAsync(CancellationToken cancellationToken)
    {
        var topology = await _topologyProvider.GetTopologyAsync(cancellationToken).ConfigureAwait(false);
        var monitors = topology.Select(candidate =>
        {
            var identity = MonitorIdentityPolicy.Resolve(candidate);
            var supportsBrightness = false;
            try
            {
                supportsBrightness = _capabilityProbe.SupportsBrightness(candidate.NativeHandle);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                supportsBrightness = false;
            }
            return new MonitorModel(
                identity.StableId,
                identity.HumanDisplayName,
                supportsBrightness,
                true, 15, 100, 0, candidate.NativeHandle,
                candidate.Bounds, candidate.WorkArea, candidate.IsPrimary,
                identity.LegacyAliases, identity.HumanDisplayName);
        }).ToList();

        if (monitors.Count == 0)
        {
            monitors.Add(new MonitorModel("primary", "Primary display", false, true, 15, 100, 0));
        }

        return monitors;
    }

    private sealed class WindowsMonitorTopologyProvider : IMonitorTopologyProvider
    {
        public ValueTask<IReadOnlyList<MonitorTopologyCandidate>> GetTopologyAsync(CancellationToken cancellationToken)
        {
            var monitors = new List<MonitorTopologyCandidate>();
            NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, (hMonitor, _, _, _) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new NativeMethods.MonitorInfoEx { cbSize = Marshal.SizeOf<NativeMethods.MonitorInfoEx>() };
                var found = NativeMethods.GetMonitorInfo(hMonitor, ref info);
                var device = found ? info.szDevice : $"Display{monitors.Count + 1}";
                var target = DisplayConfigIdentityReader.TryResolve(device);
                monitors.Add(new MonitorTopologyCandidate(
                    hMonitor.ToInt64(), device, target?.DevicePath, target?.EdidIdentity,
                    target?.WmiIdentity, target?.FriendlyName,
                    found ? Bounds(info.rcMonitor) : default,
                    found ? Bounds(info.rcWork) : default,
                    found && (info.dwFlags & 1) != 0));
                return true;
            }, nint.Zero);
            return ValueTask.FromResult<IReadOnlyList<MonitorTopologyCandidate>>(monitors);
        }

        private static DisplayBounds Bounds(NativeMethods.Rect value) =>
            new(value.Left, value.Top, value.Right - value.Left, value.Bottom - value.Top);
    }

    private sealed class DdcCapabilityProbe : IMonitorCapabilityProbe
    {
        public bool SupportsBrightness(long nativeHandle) => SupportsDdcBrightness(new nint(nativeHandle));
    }

    private static bool SupportsDdcBrightness(nint hMonitor)
    {
        try
        {
            if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) || count == 0)
            {
                return false;
            }

            var physical = new NativeMethods.PhysicalMonitor[count];
            if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical))
            {
                return false;
            }

            try
            {
                return physical.Any(item =>
                    NativeMethods.GetMonitorCapabilities(item.hPhysicalMonitor, out var capabilities, out _)
                    && (capabilities & McCapsBrightness) == McCapsBrightness);
            }
            finally
            {
                NativeMethods.DestroyPhysicalMonitors(count, physical);
            }
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }
}
