namespace ReportService.Options;

/// <summary>
/// Strongly-typed configuration for demo data seeding.
/// All club IDs must be positive — missing or zero values throw InvalidOperationException.
/// </summary>
public sealed class DemoDataOptions
{
    public bool Enabled { get; set; }
    public bool ResetReports { get; set; }
    public Dictionary<string, int> Clubs { get; set; } = new();

    public int GetClubId(string key)
    {
        if (!Clubs.TryGetValue(key, out var id) || id <= 0)
            throw new InvalidOperationException(
                $"DemoData:Clubs:{key} must be a positive integer. " +
                "Set explicit club IDs in appsettings.Development.json or appsettings.Docker.json.");
        return id;
    }

    /// <summary>
    /// Validates all club IDs are positive; throws if any are missing or non-positive.
    /// </summary>
    public (int It, int Se, int Ai, int Art) GetClubIds()
    {
        return (
            GetClubId("ItClubId"),
            GetClubId("SeClubId"),
            GetClubId("AiClubId"),
            GetClubId("ArtClubId")
        );
    }
}
