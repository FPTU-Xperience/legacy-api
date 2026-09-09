using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceService.Clients;

public sealed record FutureEventReportDetailSnapshot(int Id, string ActivityName, DateOnly ActivityDate);

public sealed record FutureEventReportSnapshot(
    int Id,
    int ClubId,
    string ClubName,
    string ReportType,
    string Status,
    int? BudgetProposalId,
    IReadOnlyCollection<FutureEventReportDetailSnapshot> Details);

public sealed class FutureEventReportClient(HttpClient httpClient, ILogger<FutureEventReportClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<FutureEventReportSnapshot?> GetAsync(int reportId, string bearerToken, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/reports/{reportId}", bearerToken);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<FutureEventReportSnapshot>(JsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Unable to resolve future event report {ReportId}.", reportId);
            return null;
        }
    }

    public async Task<bool> LinkBudgetAsync(
        int reportId,
        int budgetProposalId,
        decimal requestedAmount,
        string description,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, $"api/reports/{reportId}/link-budget", bearerToken);
        request.Content = JsonContent.Create(new { budgetProposalId, requestedAmount, description });
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Unable to link budget proposal {ProposalId} to report {ReportId}.", budgetProposalId, reportId);
            return false;
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string bearerToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return request;
    }
}
