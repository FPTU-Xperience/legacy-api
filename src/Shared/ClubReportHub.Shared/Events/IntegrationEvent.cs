namespace ClubReportHub.Shared.Events;

public interface ICorrelatedEvent
{
    string? EntityId => null;
    string? ClubId => null;
    string? Period => null;
}

public abstract record IntegrationEvent(Guid EventId, DateTimeOffset OccurredAtUtc)
{
    public string? CorrelationId { get; init; }
}

public sealed record ClubCreatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ClubId,
    string ClubCode,
    string ClubName)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ClubId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
}

public sealed record UserRegisteredEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int UserId,
    string Email,
    string FullName)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => UserId.ToString();
}

public sealed record ActivityCreatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ActivityId,
    int ClubId,
    string ClubName,
    string Title,
    DateTimeOffset StartTimeUtc,
    IReadOnlyCollection<int>? RecipientUserIds = null)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ActivityId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
}

public sealed record ReportCreatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ReportId,
    int ClubId,
    string ClubName,
    string Period,
    int CreatedByUserId,
    IReadOnlyCollection<int>? RecipientUserIds = null)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ReportId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
    string? ICorrelatedEvent.Period => Period;
}

public sealed record ReportSubmittedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ReportId,
    int ClubId,
    string ClubName,
    string Period,
    int SubmittedByUserId,
    string Status,
    int? RecipientUserId,
    string WorkflowStage = "Standard",
    IReadOnlyCollection<int>? RecipientUserIds = null)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ReportId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
    string? ICorrelatedEvent.Period => Period;
}

public sealed record ReportApprovedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ReportId,
    int ClubId,
    string ClubName,
    string Period,
    int ApprovedByUserId,
    int RecipientUserId)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ReportId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
    string? ICorrelatedEvent.Period => Period;
}

public sealed record ReportRejectedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ReportId,
    int ClubId,
    string ClubName,
    string Period,
    int RejectedByUserId,
    int RecipientUserId,
    string Feedback)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ReportId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
    string? ICorrelatedEvent.Period => Period;
}

public sealed record KpiCalculatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ClubId,
    string ClubName,
    string Period,
    decimal Points)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ClubId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
    string? ICorrelatedEvent.Period => Period;
}

public sealed record BudgetProposalSubmittedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ProposalId,
    int ClubId,
    string ClubName,
    decimal RequestedAmount,
    int ProposedByUserId,
    string ReviewStage = "ManagerReview",
    IReadOnlyCollection<int>? RecipientUserIds = null)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ProposalId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
}

public sealed record BudgetApprovedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ProposalId,
    int ClubId,
    string ClubName,
    decimal ApprovedAmount,
    int ApprovedByUserId,
    int RecipientUserId)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ProposalId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
}

public sealed record SettlementOverdueEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ProposalId,
    int ClubId,
    string ClubName)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ProposalId.ToString();
    string? ICorrelatedEvent.ClubId => ClubId.ToString();
}

public sealed record ExportRequestedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ExportRequestId,
    string ExportType,
    string Scope,
    int RequestedByUserId)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ExportRequestId.ToString();
}

public sealed record ExportCompletedEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    int ExportRequestId,
    string ExportType,
    string FileName,
    int RequestedByUserId)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.EntityId => ExportRequestId.ToString();
}

public sealed record ReportDeadlineReminderEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string Period,
    DateOnly DueDate,
    IReadOnlyCollection<int> MissingClubIds)
    : IntegrationEvent(EventId, OccurredAtUtc), ICorrelatedEvent
{
    string? ICorrelatedEvent.Period => Period;
}
