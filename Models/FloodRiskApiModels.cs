namespace FloodApp.Models;

// ── Unified success response (used by both endpoints) ────────────────────────

/// <summary>Standard flood risk assessment response.</summary>
public class FloodRiskApiResponse
{
    /// <example>true</example>
    public bool Success { get; set; } = true;

    /// <summary>"address" or "division"</summary>
    /// <example>address</example>
    public string InputType { get; set; } = "";

    /// <summary>The raw value provided by the caller.</summary>
    /// <example>Colombo 07, Sri Lanka</example>
    public string InputValue { get; set; } = "";

    /// <summary>Human-readable resolved location string.</summary>
    /// <example>Colombo 07, Western Province, Sri Lanka</example>
    public string NormalizedLocation { get; set; } = "";

    /// <summary>Latitude of the assessed location.</summary>
    /// <example>6.9271</example>
    public double Latitude { get; set; }

    /// <summary>Longitude of the assessed location.</summary>
    /// <example>79.8612</example>
    public double Longitude { get; set; }

    /// <summary>Overall flood risk level: High / Medium / Low / Unknown</summary>
    /// <example>Medium</example>
    public string FloodRiskLevel { get; set; } = "Unknown";

    /// <summary>Numeric risk score from 0.0 (no risk) to 1.0 (extreme risk).</summary>
    /// <example>0.62</example>
    public double RiskScore { get; set; }

    /// <summary>Plain-English description of the risk.</summary>
    /// <example>This area has a medium flood risk based on available flood risk data.</example>
    public string RiskDescription { get; set; } = "";

    /// <summary>Whether weather data caused a risk level escalation.</summary>
    /// <example>false</example>
    public bool WasEscalated { get; set; }

    /// <summary>Reason for escalation, if applicable.</summary>
    public string? EscalationReason { get; set; }

    /// <summary>Current rainfall at the location in mm.</summary>
    /// <example>2.5</example>
    public double CurrentRainfallMm { get; set; }

    /// <summary>ML model prediction output.</summary>
    public FloodRiskMlResult MlModelResult { get; set; } = new();

    /// <summary>Number of recorded historical flood events for this area.</summary>
    /// <example>14</example>
    public int HistoricalFloodEvents { get; set; }

    /// <summary>Average people affected per historical event.</summary>
    /// <example>234.5</example>
    public double HistoricalAvgAffected { get; set; }

    /// <summary>Description of the data sources used.</summary>
    /// <example>Flood risk database + ML model prediction</example>
    public string DataSource { get; set; } = "Flood risk database + ML model prediction";

    /// <summary>UTC timestamp of when the assessment was performed.</summary>
    /// <example>2026-05-06T10:30:00Z</example>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>ML model prediction sub-object.</summary>
public class FloodRiskMlResult
{
    /// <summary>Whether the ML prediction was available.</summary>
    /// <example>true</example>
    public bool Available { get; set; } = false;

    /// <summary>Human-readable label, e.g. "Medium Flood Risk".</summary>
    /// <example>Medium Flood Risk</example>
    public string PredictionLabel { get; set; } = "Not Available";

    /// <summary>Numeric prediction score 0.0 – 1.0.</summary>
    /// <example>0.62</example>
    public double PredictionScore { get; set; }

    /// <summary>Model confidence score 0.0 – 1.0.</summary>
    /// <example>0.87</example>
    public double Confidence { get; set; }

    /// <summary>Estimated number of people affected.</summary>
    /// <example>150</example>
    public int? PredictedAffected { get; set; }

    /// <summary>Name of the ML model used.</summary>
    /// <example>flood_risk_rf_model</example>
    public string ModelName { get; set; } = "flood_risk_rf_model";

    /// <summary>Model version identifier.</summary>
    /// <example>current</example>
    public string ModelVersion { get; set; } = "current";
}

// ── Standard error envelope ───────────────────────────────────────────────────

/// <summary>Standard error response returned on 400 / 404 / 422 / 500.</summary>
public class FloodRiskApiError
{
    /// <example>false</example>
    public bool Success { get; set; } = false;

    /// <summary>Machine-readable error code.</summary>
    /// <example>LOCATION_NOT_FOUND</example>
    public string ErrorCode { get; set; } = "";

    /// <summary>Human-readable error message.</summary>
    /// <example>The provided address could not be resolved.</example>
    public string Message { get; set; } = "";

    /// <summary>Additional detail to help the caller fix the issue.</summary>
    public string? Detail { get; set; }

    /// <summary>Available options to choose from, if applicable.</summary>
    public object? AvailableOptions { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
