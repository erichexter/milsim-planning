namespace MilsimPlanning.Api.Infrastructure.BackgroundJobs;

public interface INotificationQueue
{
    ValueTask EnqueueAsync(NotificationJob job, CancellationToken ct = default);
    IAsyncEnumerable<NotificationJob> ReadAllAsync(CancellationToken ct);
}
