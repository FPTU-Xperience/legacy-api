using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReportService.Clients;

public sealed record FinanceWorkflowSnapshot(int Id, int? SourceReportId, decimal RequestedAmount, decimal? ApprovedAmount, string Status);

public sealed class FinanceWorkflowClient(HttpClient httpClient, ILogger<FinanceWorkflowClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<FinanceWorkflowSnapshot?> ManagerApproveAsync(int proposalId, string? note, string token, CancellationToken ct) =>
        PostAsync(proposalId, "manager-approve", new { approvedAmount = (decimal?)null, note }, token, ct);

    public Task<FinanceWorkflowSnapshot?> ManagerRejectAsync(int proposalId, string? note, string token, CancellationToken ct) =>
        PostAsync(proposalId, "manager-reject", new { approvedAmount = (decimal?)null, note }, token, ct);

    public Task<FinanceWorkflowSnapshot?> FinalApproveAsync(int proposalId, decimal approvedAmount, string? note, string token, CancellationToken ct) =>
        PostAsync(proposalId, "approve", new { approvedAmount = (decimal?)approvedAmount, note }, token, ct);

    public Task<FinanceWorkflowSnapshot?> FinalRejectAsync(int proposalId, string? note, string token, CancellationToken ct) =>
        PostAsync(proposalId, "reject", new { approvedAmount = (decimal?)null, note }, token, ct);

    private async Task<FinanceWorkflowSnapshot?> PostAsync(
        int proposalId,
        string action,
        object payload,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/finance/proposals/{proposalId}/{action}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Add("X-Combined-Report-Workflow", "true");
        request.Content = JsonContent.Create(payload);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Finance workflow action {Action} failed for proposal {ProposalId} with {StatusCode}.", action, proposalId, response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<FinanceWorkflowSnapshot>(JsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Finance workflow action {Action} failed for proposal {ProposalId}.", action, proposalId);
            return null;
        }
    }
}
