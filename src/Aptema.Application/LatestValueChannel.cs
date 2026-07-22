using System.Threading.Channels;

namespace Aptema.Application;

public interface ILatestValueChannel<T>
{
    bool TryPublish(T value);
    bool TryRead(out T? value);
    ValueTask<T> ReadAsync(CancellationToken cancellationToken);
}

public sealed class LatestValueChannel<T> : ILatestValueChannel<T>
{
    private readonly Channel<T> _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = false,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    public bool TryPublish(T value) => _channel.Writer.TryWrite(value);

    public bool TryRead(out T? value) => _channel.Reader.TryRead(out value);

    public ValueTask<T> ReadAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAsync(cancellationToken);
}
