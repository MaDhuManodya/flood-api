namespace FloodApp.Models;

/// <summary>
/// Request body for POST /api/flood-risk.
/// Provide either 'address' or 'division' (or both — address takes priority).
/// </summary>
public class FloodRiskRequest
{
    /// <summary>Free-text address string, e.g. "Colombo 07, Sri Lanka".</summary>
    public string? Address { get; set; }

    /// <summary>Administrative division / Divisional Secretariat name, e.g. "Kaduwela".</summary>
    public string? Division { get; set; }
}
