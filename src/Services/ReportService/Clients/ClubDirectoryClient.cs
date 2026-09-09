using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReportService.Clients;

public sealed record ClubDirectorySnapshot(int Id, string Name, bool IsActive);

public sealed class ClubDirectoryClient(HttpClient httpClient, ILogger<ClubDirectoryClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<ClubDirectorySnapshot>> GetAllAsync(
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/clubs");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Club directory lookup failed with {StatusCode}.", response.StatusCode);
                return [];
            }

            return await response.Content.ReadFromJsonAsync<List<ClubDirectorySnapshot>>(JsonOptions, cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Club directory lookup failed.");
            return [];
        }
    }
}
