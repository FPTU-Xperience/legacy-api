using Grpc.Core;
using KpiGrpcService.Protos;

namespace KpiGrpcService.Services;

/// <summary>
/// gRPC server implementation for KPI calculations.
/// Logic mirrors the KPI rules from ReportService:
/// - APPROVED_REPORT: +50 per approved report
/// - ACTIVITY: +5 per activity detail
/// - PARTICIPATION: +0.1 per participant
/// - REJECTED_REPORT: -10 penalty
/// - OVERDUE_REPORT: -20 penalty
/// </summary>
public sealed class KpiServiceImpl : KpiService.KpiServiceBase
{
    private readonly ILogger<KpiServiceImpl> _logger;

    private const decimal PointsPerApprovedReport = 50m;
    private const decimal PointsPerActivity = 5m;
    private const decimal PointsPerParticipant = 0.1m;
    private const decimal PenaltyRejectedReport = -10m;
    private const decimal PenaltyOverdueReport = -20m;
    private const string CalculationVersion = "1.0-gRPC";

    public KpiServiceImpl(ILogger<KpiServiceImpl> logger)
    {
        _logger = logger;
    }

    public override async Task<KpiClubResponse> CalculateClubKpi(
        KpiClubRequest request,
        ServerCallContext context)
    {
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? "N/A" : request.CorrelationId;
        _logger.LogInformation(
            "CalculateClubKpi RPC called. ClubId: {ClubId}, ClubName: '{ClubName}', Period: '{Period}', CorrelationId: '{CorrelationId}'",
            request.ClubId,
            request.ClubName,
            string.IsNullOrWhiteSpace(request.Period) ? "(all)" : request.Period,
            correlationId);

        // Validate request
        if (request.ClubId <= 0)
        {
            _logger.LogWarning("CalculateClubKpi invalid argument: ClubId must be positive. ClubId={ClubId}, CorrelationId: '{CorrelationId}'",
                request.ClubId, correlationId);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ClubId must be positive."));
        }

        var totalScore = CalculateScore(
            request.ApprovedReports,
            request.ActivityCount,
            request.ParticipantCount,
            request.RejectedReports,
            request.OverdueReports);

        var rating = DetermineRating(totalScore);

        var response = new KpiClubResponse
        {
            ClubId = request.ClubId,
            ClubName = request.ClubName,
            TotalScore = (double)Math.Max(0, decimal.Round(totalScore, 2)),
            Rating = rating,
            ApprovedReports = request.ApprovedReports,
            ActivityCount = request.ActivityCount,
            ParticipantCount = request.ParticipantCount,
            CalculatedAtUtc = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime),
            CalculationVersion = CalculationVersion
        };

        _logger.LogInformation(
            "CalculateClubKpi RPC completed. ClubId: {ClubId}, TotalScore: {TotalScore}, Rating: '{Rating}', CorrelationId: '{CorrelationId}'",
            request.ClubId,
            response.TotalScore,
            rating,
            correlationId);

        return response;
    }

    /// <summary>
    /// Pure calculation method — deterministic and unit-testable.
    /// </summary>
    public static decimal CalculateScore(
        int approvedReports,
        int activityCount,
        int participantCount,
        int rejectedReports,
        int overdueReports)
    {
        var score = approvedReports * PointsPerApprovedReport
                  + activityCount * PointsPerActivity
                  + participantCount * PointsPerParticipant
                  + rejectedReports * PenaltyRejectedReport
                  + overdueReports * PenaltyOverdueReport;

        return Math.Max(0, decimal.Round(score, 2));
    }

    /// <summary>
    /// Determines rating based on total score.
    /// </summary>
    public static string DetermineRating(decimal totalScore)
    {
        return totalScore switch
        {
            >= 500 => "Excellent",
            >= 200 => "Good",
            >= 50 => "Average",
            _ => "Needs Improvement"
        };
    }
}
