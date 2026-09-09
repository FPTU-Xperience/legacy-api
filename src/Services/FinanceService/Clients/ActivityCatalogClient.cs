using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceService.Clients;

public sealed record FinanceActivitySnapshot(
    int Id,
    int ClubId,
    string Title,
    string Description,
    IReadOnlyCollection<int> MeetingDays,
    string Status);

public sealed class ActivityCatalogClient(HttpClient httpClient, ILogger<ActivityCatalogClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<FinanceActivitySnapshot?> GetAsync(
        int activityId,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/activities/{activityId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<FinanceActivitySnapshot>(JsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Unable to resolve activity {ActivityId} for a budget proposal.", activityId);
            return null;
        }
    }
}
