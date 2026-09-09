namespace ClubReportHub.Shared.Messaging;

public static class EventRoutingKeys
{
    public const string ClubCreated = "club.created";
    public const string UserRegistered = "user.registered";
    public const string ActivityCreated = "activity.created";
    public const string ReportCreated = "report.created";
    public const string ReportSubmitted = "report.submitted";
    public const string ReportApproved = "report.approved";
    public const string ReportRejected = "report.rejected";
    public const string KpiCalculated = "kpi.calculated";
    public const string BudgetProposalSubmitted = "budget.proposal.submitted";
    public const string BudgetApproved = "budget.approved";
    public const string SettlementOverdue = "settlement.overdue";
    public const string ExportRequested = "export.requested";
    public const string ExportCompleted = "export.completed";
    public const string ReportDeadlineReminder = "report.deadline.reminder";
}
