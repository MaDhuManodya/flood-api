namespace FloodApp.Models;

// ── Success response ─────────────────────────────────────────────────────────

public class FloodRiskResponse
{
    public bool Success { get; set; } = true;

    /// <summary>"address" or "division"</summary>
    public string InputType { get; set; } = "";

    /// <summary>The raw value the caller sent.</summary>
    public string InputValue { get; set; } = "";

    /// <summary>Human-readable resolved location string.</summary>
    public string NormalizedLocation { get; set; } = "";

    /// <summary>Overall flood risk level: High / Medium / Low / Unknown</summary>
    public string FloodRiskLevel { get; set; } = "Unknown";

    /// <summary>Numeric risk score 0.0 – 1.0.</summary>
    public double RiskScore { get; set; }

    /// <summary>Plain-English description of the risk.</summary>
    public string RiskDescription { get; set; } = "";

    /// <summary>Whether weather data caused a risk level escalation.</summary>
    public bool WasEscalated { get; set; }

    /// <summary>Reason for escalation, if applicable.</summary>
    public string? EscalationReason { get; set; }

    /// <summary>Current rainfall at the location (mm).</summary>
    public double CurrentRainfallMm { get; set; }

    /// <summary>Full ML model output.</summary>
    public MlModelResult MlModelResult { get; set; } = new();

    /// <summary>Number of historical flood events recorded for this area.</summary>
    public int HistoricalFloodEvents { get; set; }

    /// <summary>Average people affected per historical event.</summary>
    public double HistoricalAvgAffected { get; set; }

    /// <summary>Coordinates used for the assessment.</summary>
    public LocationCoords Coordinates { get; set; } = new();

    public string DataSource { get; set; } = "Flood risk database + ML model prediction";

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class MlModelResult
{
    /// <summary>Human-readable label, e.g. "High Flood Risk".</summary>
    public string PredictionLabel { get; set; } = "Not Available";

    /// <summary>Numeric score 0.0 – 1.0 mapped from severity.</summary>
    public double PredictionScore { get; set; }

    /// <summary>Confidence score 0.0 – 1.0 derived from historical data volume.</summary>
    public double Confidence { get; set; }

    /// <summary>Estimated number of people affected.</summary>
    public int? PredictedAffected { get; set; }

    public string ModelName { get; set; } = "flood_risk_rf_model";
    public string ModelVersion { get; set; } = "current";

    /// <summary>Whether the ML prediction succeeded.</summary>
    public bool Available { get; set; } = false;
}

public class LocationCoords
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

// ── Error response ────────────────────────────────────────────────────────────

public class FloodRiskError
{
    public bool Success { get; set; } = false;
    public string ErrorCode { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Detail { get; set; }
    public object? AvailableOptions { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
