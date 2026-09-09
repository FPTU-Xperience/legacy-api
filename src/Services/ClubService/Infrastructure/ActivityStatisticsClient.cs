using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ClubService.Infrastructure;

public sealed record ActivityMemberInput(int UserId, DateTimeOffset JoinedAtUtc);
public sealed record ActivityStatisticsQuery(IReadOnlyCollection<ActivityMemberInput> Members);
public sealed record ActivityMemberStatistics(int UserId, int EligibleActivities, int AttendedActivities, decimal ParticipationRate);
public sealed record ActivityStatisticsDetailQuery(int UserId, DateTimeOffset JoinedAtUtc, int Page, int PageSize);
public sealed record ActivityHistoryItem(int ActivityId, string Title, DateTimeOffset StartTimeUtc, string ActivityStatus, string AttendanceStatus);
public sealed record ActivityStatisticsDetail(ActivityMemberStatistics Statistics, IReadOnlyCollection<ActivityHistoryItem> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public sealed class ActivityStatisticsClient(HttpClient httpClient)
{
    public async Task<IReadOnlyDictionary<int, ActivityMemberStatistics>> GetBatchAsync(
        int clubId,
        IReadOnlyCollection<ActivityMemberInput> members,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/activities/clubs/{clubId}/member-statistics")
        {
            Content = JsonContent.Create(new ActivityStatisticsQuery(members))
        };
        SetBearer(request, bearerToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActivityMemberStatistics>>(cancellationToken: cancellationToken) ?? [];
        return rows.ToDictionary(x => x.UserId);
    }

    public async Task<ActivityStatisticsDetail> GetDetailAsync(
        int clubId,
        ActivityStatisticsDetailQuery query,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/activities/clubs/{clubId}/member-statistics/detail")
        {
            Content = JsonContent.Create(query)
        };
        SetBearer(request, bearerToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityStatisticsDetail>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("Activity Service returned an empty statistics response.");
    }

    private static void SetBearer(HttpRequestMessage request, string? bearerToken)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }
}
