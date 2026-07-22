using LightPilot.Core;
using LightPilot.Application;

namespace LightPilot.Infrastructure;

public interface ISettingsStore : LightPilot.Application.ISettingsStore
{
}

public interface IMonitorEnumerator : LightPilot.Application.IMonitorEnumerator
{
}

public interface IBrightnessController : LightPilot.Application.IBrightnessController
{
}

public interface IDdcCiApi
{
    ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken);
}

public interface IWindowsBrightnessApi
{
    ValueTask<bool> TrySetBrightnessAsync(MonitorModel monitor, int brightnessPercent, CancellationToken cancellationToken);
}

public interface IOverlayController
{
    ValueTask ApplyAsync(MonitorModel monitor, double opacity, int colorTemperatureKelvin, CancellationToken cancellationToken);
}

public interface IForegroundWindowDetector : LightPilot.Application.IForegroundWindowDetector
{
}

public interface IFullscreenDetector
{
    bool IsFullscreen(WindowSnapshot snapshot);
}

public interface IContentLuminanceSampler : LightPilot.Application.IContentLuminanceSampler
{
}

public interface IPowerStatusProvider : LightPilot.Application.IPowerStatusProvider
{
}
