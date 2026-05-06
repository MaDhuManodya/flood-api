namespace FloodApp.Models;

/// <summary>
/// Unified request body for POST /api/v1/calculate-risk.
/// Send either Type A (address_string) or Type B (province/district/divisional_secretariat).
/// company_id is required in both cases.
/// </summary>
public class CalculateRiskRequest
{
    // ── Required for both types ──────────────────────────────────────────
    /// <summary>
    /// Unique identifier of the company calling the API.
    /// Used for multi-tenant tracking and audit logging.
    /// </summary>
    public string CompanyId { get; set; } = "";

    // ── Type A: Manual Address ───────────────────────────────────────────
    /// <summary>
    /// Free-text address string (e.g. "No. 10, Galle Road, Colombo 03").
    /// Provide this OR the hierarchical fields — not both.
    /// </summary>
    public string? AddressString { get; set; }

    // ── Type B: Hierarchical Selection ──────────────────────────────────
    /// <summary>Province name (e.g. "Western").</summary>
    public string? Province { get; set; }

    /// <summary>District name (e.g. "Colombo").</summary>
    public string? District { get; set; }

    /// <summary>Divisional Secretariat name (e.g. "Kaduwela").</summary>
    public string? DivisionalSecretariat { get; set; }
}
