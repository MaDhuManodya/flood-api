namespace FloodApp.Models;

/// <summary>
/// Request body for the POST /api/risk/assess endpoint.
/// Provide either Address (direct input) OR Province+District+Division (hierarchical selection).
/// </summary>
public class RiskAssessmentRequest
{
    // ── Method 1: Direct Address Input ──
    /// <summary>
    /// A free-text address string (e.g. "Colombo Fort", "Galle Road, Panadura").
    /// When provided, the system geocodes this to lat/lng and calculates risk.
    /// </summary>
    public string? Address { get; set; }

    // ── Method 2: Hierarchical Selection ──
    /// <summary>Province name (e.g. "Western", "Southern").</summary>
    public string? Province { get; set; }

    /// <summary>District name (e.g. "Colombo", "Galle").</summary>
    public string? District { get; set; }

    /// <summary>Divisional Secretariat name (e.g. "Kaduwela", "Homagama").</summary>
    public string? Division { get; set; }
}
