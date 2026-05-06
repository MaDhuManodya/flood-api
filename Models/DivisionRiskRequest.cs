namespace FloodApp.Models;

/// <summary>Request body for POST /api/flood-risk/division</summary>
public class DivisionRiskRequest
{
    /// <summary>
    /// A Sri Lankan Divisional Secretariat name.
    /// Example: "Kaduwela"
    /// Use GET /api/flood-risk/locations to list all valid values.
    /// </summary>
    public string? Division { get; set; }
}
