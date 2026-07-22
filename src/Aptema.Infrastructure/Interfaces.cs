using Aptema.Core;
using Aptema.Application;

namespace Aptema.Infrastructure;

public interface ISettingsStore : Aptema.Application.ISettingsStore
{
}

public interface IMonitorEnumerator : Aptema.Application.IMonitorEnumerator
{
}

public interface IBrightnessController : Aptema.Application.IBrightnessController
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

public interface IForegroundWindowDetector : Aptema.Application.IForegroundWindowDetector
{
}

public interface IFullscreenDetector
{
    bool IsFullscreen(WindowSnapshot snapshot);
}

public interface IContentLuminanceSampler : Aptema.Application.IContentLuminanceSampler
{
}

public interface IPowerStatusProvider : Aptema.Application.IPowerStatusProvider
{
}
