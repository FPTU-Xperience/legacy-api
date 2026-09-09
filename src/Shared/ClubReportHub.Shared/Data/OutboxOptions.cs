namespace ClubReportHub.Shared.Data;

public sealed class OutboxOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);
    public int BatchSize { get; set; } = 20;
    public int MaxRetries { get; set; } = 5;
}
