using System.Threading.Channels;
using Invensys.ITrace.Contracts;

namespace Invensys.ITrace.Client;

internal sealed class ITraceTelemetryQueue(int capacity)
{
    private readonly Channel<TelemetryEnvelope> channel = Channel.CreateBounded<TelemetryEnvelope>(
        new BoundedChannelOptions(Math.Max(capacity, 1))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    public bool TryWrite(TelemetryEnvelope envelope, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return channel.Writer.TryWrite(envelope);
    }

    public IAsyncEnumerable<TelemetryEnvelope> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);
}
