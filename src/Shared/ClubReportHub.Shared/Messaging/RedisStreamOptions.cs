namespace ClubReportHub.Shared.Messaging;

public sealed class RedisStreamOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; init; } = "localhost:6379";

    /// <summary>
    /// Name of the Redis stream used for all ClubReportHub events.
    /// </summary>
    public string StreamName { get; init; } = "clubreporthub-events";

    /// <summary>
    /// Consumer group name used by the NotificationService.
    /// </summary>
    public string ConsumerGroup { get; init; } = "notification-service";

    /// <summary>
    /// Prefix for consumer instance names (e.g. "notification-service-instance-{MachineName}-{ProcessId}").
    /// </summary>
    public string ConsumerNamePrefix { get; init; } = "consumer";

    /// <summary>
    /// Number of messages to read per XREADGROUP call.
    /// </summary>
    public int BatchSize { get; init; } = 5;

    /// <summary>
    /// Polling interval in milliseconds when the stream is empty.
    /// </summary>
    public int PollIntervalMs { get; init; } = 1000;

    /// <summary>
    /// Maximum retry attempts when a Redis operation fails.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Base delay in milliseconds for exponential back-off between retries.
    /// </summary>
    public int RetryBaseDelayMs { get; init; } = 500;

    /// <summary>
    /// Maximum times a message can be delivered/retried before being moved to the dead-letter queue.
    /// </summary>
    public int MaxDeliveryAttempts { get; init; } = 3;

    /// <summary>
    /// Name of the Redis stream used for dead-lettered events that failed processing.
    /// </summary>
    public string DeadLetterStreamName { get; init; } = "clubreporthub-events-dlq";
}
