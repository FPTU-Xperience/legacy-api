using System.Net.Http.Headers;
using System.Net.Http.Json;
using ActivityService.Contracts;

namespace ActivityService.Infrastructure;

public sealed record ClubMemberRosterItem(int Id, int UserId, string FullName, string Email, string PhoneNumber, string Role, string Status, DateTimeOffset JoinedAtUtc);
public sealed record ClubMemberRosterPage(IReadOnlyCollection<ClubMemberRosterItem> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record ResolveRosterRequest(IReadOnlyCollection<int> MemberIds, DateTimeOffset? JoinedOnOrBefore);

public sealed class ClubMemberRosterClient(HttpClient httpClient)
{
    public async Task<ClubMemberRosterPage> GetAsync(
        int clubId,
        DateTimeOffset joinedOnOrBefore,
        string? search,
        int page,
        int pageSize,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/clubs/{clubId}/member-roster?joinedOnOrBefore={Uri.EscapeDataString(joinedOnOrBefore.ToString("O"))}&search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}");
        SetBearer(request, bearerToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ClubMemberRosterPage>(cancellationToken: cancellationToken)
            ?? new ClubMemberRosterPage([], page, pageSize, 0, 0);
    }

    public async Task<IReadOnlyCollection<ClubMemberRosterItem>> ResolveAsync(
        int clubId,
        IReadOnlyCollection<int> memberIds,
        DateTimeOffset joinedOnOrBefore,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/clubs/{clubId}/member-roster/resolve")
        {
            Content = JsonContent.Create(new ResolveRosterRequest(memberIds, joinedOnOrBefore))
        };
        SetBearer(request, bearerToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ClubMemberRosterItem>>(cancellationToken: cancellationToken) ?? [];
    }

    private static void SetBearer(HttpRequestMessage request, string? bearerToken)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Club roster request failed ({(int)response.StatusCode}): {detail}", null, response.StatusCode);
    }
}
