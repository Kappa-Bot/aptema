using Aptema.Application;
using Aptema.Core;

namespace Aptema.App.Services;

public sealed class DisabledContentLuminanceSampler : IContentLuminanceSampler
{
    public ValueTask<ContentLuminanceSample> SampleAsync(bool enabled, CancellationToken cancellationToken) =>
        ValueTask.FromResult(ContentLuminanceSample.Unknown);
}
