using Aptema.Core;
using Aptema.Application;

namespace Aptema.App.Services;

public sealed class NoOpBrightnessController : IBrightnessController
{
    public ValueTask<BrightnessApplyResult> ApplyAsync(MonitorModel monitor, ComfortDecision decision, UserSettings settings, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(BrightnessApplyResult.NoChange(monitor.Id, "NoOpController"));
    }
}
