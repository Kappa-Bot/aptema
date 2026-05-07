using LightPilot.Core;

namespace LightPilot.Infrastructure;

public interface ISettingsStore
{
    ValueTask<UserSettings> LoadAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(UserSettings settings, CancellationToken cancellationToken);
}

public interface IMonitorEnumerator
{
    ValueTask<IReadOnlyList<MonitorModel>> EnumerateAsync(CancellationToken cancellationToken);
}

public interface IBrightnessController
{
    ValueTask ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken);
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

public interface IForegroundWindowDetector
{
    AppContextModel Detect();
}

public interface IFullscreenDetector
{
    bool IsFullscreen(WindowSnapshot snapshot);
}

public interface IContentLuminanceSampler
{
    ValueTask<ContentLuminanceSample> SampleAsync(bool enabled, CancellationToken cancellationToken);
}

public interface IPowerStatusProvider
{
    bool IsOnBattery();
}
