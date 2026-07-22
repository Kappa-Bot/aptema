using Aptema.Application;

namespace Aptema.Application.Tests;

public sealed class LatestValueChannelTests
{
    [Fact]
    public async Task ReadAsyncReturnsNewestPendingValue()
    {
        var channel = new LatestValueChannel<int>();

        Assert.True(channel.TryPublish(1));
        Assert.True(channel.TryPublish(2));
        Assert.True(channel.TryPublish(3));

        Assert.Equal(3, await channel.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public void TryReadReturnsFalseWhenNoValueIsPending()
    {
        var channel = new LatestValueChannel<string>();

        Assert.False(channel.TryRead(out _));
    }
}
