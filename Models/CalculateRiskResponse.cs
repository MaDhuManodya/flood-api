namespace FloodApp.Models;

/// <summary>
/// Standard response envelope for POST /api/v1/calculate-risk.
/// </summary>
public class CalculateRiskResponse
{
    // ── Request Context ──────────────────────────────────────────────────
    /// <summary>Company ID echoed from the request for traceability.</summary>
    public string CompanyId { get; set; } = "";

    /// <summary>Input type used: "address" or "hierarchical".</summary>
    public string InputType { get; set; } = "";

    /// <summary>The resolved human-readable location string.</summary>
    public string ResolvedLocation { get; set; } = "";

    // ── Risk Result ──────────────────────────────────────────────────────
    /// <summary>Risk level: HIGH, MEDIUM, LOW, or UNKNOWN.</summary>
    public string RiskLevel { get; set; } = "UNKNOWN";

    /// <summary>Numeric risk score from 0 (no risk) to 10 (extreme risk).</summary>
    public double RiskScore { get; set; }

    /// <summary>Color code for UI rendering (#hex).</summary>
    public string RiskColor { get; set; } = "#6B7280";

    /// <summary>Human-readable risk description.</summary>
    public string RiskMessage { get; set; } = "";

    /// <summary>Whether rainfall data caused a risk level escalation.</summary>
    public bool WasEscalated { get; set; }

    /// <summary>Reason for escalation if applicable.</summary>
    public string? EscalationReason { get; set; }

    // ── Supporting Data ──────────────────────────────────────────────────
    /// <summary>Current rainfall at the location in mm.</summary>
    public double CurrentRainfallMm { get; set; }

    /// <summary>ML model predicted severity (HIGH/MEDIUM/LOW), if available.</summary>
    public string? MlPredictedSeverity { get; set; }

    /// <summary>ML model predicted number of people affected, if available.</summary>
    public int? MlPredictedAffected { get; set; }

    /// <summary>Number of recorded historical flood events for this area.</summary>
    public int HistoricalFloodEvents { get; set; }

    /// <summary>Average people affected per historical event.</summary>
    public double HistoricalAvgAffected { get; set; }

    // ── Coordinates ─────────────────────────────────────────────────────
    /// <summary>Latitude of the assessed location.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude of the assessed location.</summary>
    public double Longitude { get; set; }

    // ── Metadata ─────────────────────────────────────────────────────────
    /// <summary>API version.</summary>
    public string ApiVersion { get; set; } = "v1";

    /// <summary>UTC timestamp of the assessment.</summary>
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Standard error envelope returned on 400/404.
/// </summary>
public class CalculateRiskError
{
    public string Error { get; set; } = "";
    public string? Detail { get; set; }
    public object? AvailableOptions { get; set; }
    public string ApiVersion { get; set; } = "v1";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
