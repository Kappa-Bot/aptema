using System.Management;
using Aptema.Core;

namespace Aptema.Infrastructure;

public sealed class WindowsBrightnessApi : IWindowsBrightnessApi
{
    public async ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken)
    {
        return await Task.Run(() => TrySetBrightness(brightnessPercent), cancellationToken).ConfigureAwait(false);
    }

    private static bool TrySetBrightness(int brightnessPercent)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            using var results = searcher.Get();
            var wrote = false;

            foreach (ManagementObject method in results.Cast<ManagementObject>())
            {
                using (method)
                {
                    method.InvokeMethod("WmiSetBrightness", new object[] { 1u, (byte)Math.Clamp(brightnessPercent, 0, 100) });
                    wrote = true;
                }
            }

            return wrote;
        }
        catch (ManagementException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
