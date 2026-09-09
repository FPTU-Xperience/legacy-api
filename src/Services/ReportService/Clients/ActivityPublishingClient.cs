using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReportService.Clients;

public sealed record PublishedActivitySnapshot(int Id);

public sealed class ActivityPublishingClient(HttpClient httpClient, ILogger<ActivityPublishingClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PublishedActivitySnapshot?> PublishAsync(
        object payload,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/activities/from-approved-report");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = JsonContent.Create(payload);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Publishing an approved future event failed with {StatusCode}.", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<PublishedActivitySnapshot>(JsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Publishing an approved future event failed.");
            return null;
        }
    }
}
