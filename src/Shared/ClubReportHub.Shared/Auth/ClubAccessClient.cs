using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClubReportHub.Shared.Auth;

public sealed record ClubAccessSnapshot(
    int ClubId,
    string ClubName,
    bool IsManager,
    bool IsTreasurer,
    bool IsApprovedMember,
    IReadOnlyCollection<int> ManagerUserIds,
    IReadOnlyCollection<int>? MemberUserIds = null,
    IReadOnlyCollection<int>? TreasurerUserIds = null)
{
    public bool CanManage => IsManager;
    public bool CanManageFinance => IsTreasurer;
    public bool CanView => IsManager || IsApprovedMember;
}

public sealed class ClubAccessClient(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<ClubAccessClient> logger)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<ClubAccessSnapshot>> GetMyAccessAsync(
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return [];
        }

        // Extract user ID from token for cache key
        var userId = ExtractUserIdFromToken(bearerToken);
        if (userId <= 0)
        {
            return [];
        }

        var cacheKey = $"ClubAccess_{userId}";

        // Try to get from cache first
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<ClubAccessSnapshot>? cachedAccess) && cachedAccess is not null)
        {
            return cachedAccess;
        }

        // Fetch from API
        var access = await FetchAccessFromApiAsync(bearerToken, cancellationToken);

        // Cache the result with sliding expiration
        if (access.Count > 0)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(CacheDuration)
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(15))
                .SetSize(1)
                .SetPriority(CacheItemPriority.Normal);
            cache.Set(cacheKey, access, cacheOptions);
        }

        return access;
    }

    public void InvalidateCache(int userId)
    {
        var cacheKey = $"ClubAccess_{userId}";
        cache.Remove(cacheKey);
    }

    public void InvalidateCacheForAllUsers()
    {
        // In production, you might want to use Redis or a distributed cache
        // For IMemoryCache, we rely on TTL expiration
    }

    private async Task<IReadOnlyList<ClubAccessSnapshot>> FetchAccessFromApiAsync(
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/clubs/me/access");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Club access lookup failed with status code {StatusCode}.", response.StatusCode);
                return [];
            }

            return await response.Content.ReadFromJsonAsync<List<ClubAccessSnapshot>>(JsonOptions, cancellationToken) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Club access lookup failed.");
            return [];
        }
    }

    private static int ExtractUserIdFromToken(string bearerToken)
    {
        try
        {
            // Parse JWT without validation to get user ID for cache key
            var parts = bearerToken.Split('.');
            if (parts.Length != 3)
            {
                return 0;
            }

            var payload = parts[1];
            // Add padding if needed for Base64 decoding
            var padded = payload.Length % 4 == 0 ? payload : payload + new string('=', 4 - payload.Length % 4);
            // Replace URL-safe characters
            padded = padded.Replace('-', '+').Replace('_', '/');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("sub", out var subElement))
            {
                return int.TryParse(subElement.GetString(), out var userId) ? userId : 0;
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}

public static class ClubAccessServiceCollectionExtensions
{
    public static IServiceCollection AddClubAccessClient(this IServiceCollection services, IConfiguration configuration)
    {
        // Add memory cache if not already registered
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 10000; // Max 10000 entries
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
        });

        services.AddHttpClient<ClubAccessClient>(client =>
        {
            var baseUrl = configuration["Services:ClubService:BaseUrl"] ?? "http://localhost:5102";
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddStandardResilienceHandler();

        return services;
    }
}

public static class ClubAccessHttpContextExtensions
{
    public static string GetBearerToken(this HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        return authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearerPrefix.Length..].Trim()
            : string.Empty;
    }
}
