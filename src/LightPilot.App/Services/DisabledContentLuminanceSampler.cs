using LightPilot.Application;
using LightPilot.Core;

namespace LightPilot.App.Services;

public sealed class DisabledContentLuminanceSampler : IContentLuminanceSampler
{
    public ValueTask<ContentLuminanceSample> SampleAsync(bool enabled, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ContentLuminanceSample.Unknown);
}
