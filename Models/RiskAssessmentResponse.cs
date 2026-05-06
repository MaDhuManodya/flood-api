namespace FloodApp.Models;

/// <summary>
/// Comprehensive response from the POST /api/risk/assess endpoint.
/// Contains risk assessment, weather context, ML prediction, and historical data.
/// </summary>
public class RiskAssessmentResponse
{
    // ── Input Echo ──
    /// <summary>Which method was used: "address" or "hierarchical".</summary>
    public string InputMethod { get; set; } = "";

    /// <summary>Resolved address (from geocoding or hierarchical lookup).</summary>
    public string ResolvedLocation { get; set; } = "";

    /// <summary>Latitude used for the assessment.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude used for the assessment.</summary>
    public double Longitude { get; set; }

    // ── Risk Assessment ──
    /// <summary>Overall risk level: HIGH, MEDIUM, LOW, or UNKNOWN.</summary>
    public string RiskLevel { get; set; } = "UNKNOWN";

    /// <summary>Numeric risk score (0–10).</summary>
    public double RiskScore { get; set; }

    /// <summary>Human-readable risk description.</summary>
    public string Message { get; set; } = "";

    /// <summary>Risk color code for UI rendering.</summary>
    public string ColorCode { get; set; } = "#6B7280";

    /// <summary>Whether the risk was escalated due to weather conditions.</summary>
    public bool WasEscalated { get; set; }

    /// <summary>Reason for escalation, if applicable.</summary>
    public string? EscalationReason { get; set; }

    // ── Weather Context ──
    /// <summary>Current rainfall in mm at the location.</summary>
    public double CurrentRainfallMm { get; set; }

    // ── ML Prediction ──
    /// <summary>ML-predicted severity (HIGH / MEDIUM / LOW), if available.</summary>
    public string? MlPredictedSeverity { get; set; }

    /// <summary>ML-predicted number of people affected, if available.</summary>
    public int? MlPredictedAffected { get; set; }

    // ── Historical Data ──
    /// <summary>Number of historical flood events in this area.</summary>
    public int HistoricalEventCount { get; set; }

    /// <summary>Average number of people affected per historical event.</summary>
    public double HistoricalAvgAffected { get; set; }

    /// <summary>Average severity score from historical data.</summary>
    public double HistoricalAvgSeverity { get; set; }

    /// <summary>Average houses damaged per historical event.</summary>
    public double HistoricalAvgHousesDamaged { get; set; }

    // ── Metadata ──
    /// <summary>UTC timestamp of the assessment.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
