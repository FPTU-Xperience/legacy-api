using ClubService.Models;

namespace ClubService.Services;

public static class ClubMemberQuery
{
    public static IQueryable<ClubMembership> ApplyFilters(
        IQueryable<ClubMembership> query,
        string? search,
        string? status,
        string? role)
    {
        if (string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == ClubMembershipStatuses.Approved);
        else if (!status.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == status.Trim());

        if (!string.IsNullOrWhiteSpace(role) && !role.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Role == role.Trim().ToUpper());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.FullName.Contains(term) || x.Email.Contains(term) || x.PhoneNumber.Contains(term));
        }
        return query;
    }

    public static IQueryable<ClubMembership> ApplySort(
        IQueryable<ClubMembership> query,
        string? sortBy,
        string? sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("email", false) => query.OrderBy(x => x.Email).ThenBy(x => x.Id),
            ("email", true) => query.OrderByDescending(x => x.Email).ThenBy(x => x.Id),
            ("joinedat", false) => query.OrderBy(x => x.ReviewedAtUtc ?? x.RequestedAtUtc).ThenBy(x => x.Id),
            ("joinedat", true) => query.OrderByDescending(x => x.ReviewedAtUtc ?? x.RequestedAtUtc).ThenBy(x => x.Id),
            ("role", false) => query.OrderBy(x => x.Role).ThenBy(x => x.FullName),
            ("role", true) => query.OrderByDescending(x => x.Role).ThenBy(x => x.FullName),
            ("name", true) => query.OrderByDescending(x => x.FullName).ThenBy(x => x.Id),
            _ => query.OrderBy(x => x.FullName).ThenBy(x => x.Id)
        };
    }
}
