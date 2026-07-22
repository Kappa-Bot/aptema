using System.Runtime.InteropServices;

namespace Aptema.Infrastructure;

public sealed class SystemPowerStatusProvider : IPowerStatusProvider
{
    public bool IsOnBattery()
    {
        return GetSystemPowerStatus(out var status) && status.AcLineStatus == 0;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
