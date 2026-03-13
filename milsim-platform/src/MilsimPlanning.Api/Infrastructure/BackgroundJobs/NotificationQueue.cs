using System.Threading.Channels;

namespace MilsimPlanning.Api.Infrastructure.BackgroundJobs;

public class NotificationQueue : INotificationQueue
{
    private readonly Channel<NotificationJob> _channel = Channel.CreateBounded<NotificationJob>(
        new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask EnqueueAsync(NotificationJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<NotificationJob> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
