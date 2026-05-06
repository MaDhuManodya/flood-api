using FloodApp.Models;

namespace FloodApp.Services;

// ── Result types passed between service and endpoint ─────────────────────────

public class FloodRiskServiceResult
{
    public bool Success { get; set; }
    public FloodRiskResponse? Response { get; set; }
    public FloodRiskError? Error { get; set; }
    public int HttpStatusCode { get; set; } = 200;
}

// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Core business logic service for flood risk assessment.
/// Encapsulates geocoding, geo-risk calculation, ML prediction,
/// and historical data — used by the /api/flood-risk endpoint.
/// Keeps the endpoint thin and this service reusable.
/// </summary>
public class FloodRiskService
{
    private readonly GeocodingService _geocoding;
    private readonly RiskService _riskService;
    private readonly WeatherService _weather;
    private readonly MLPredictionService _ml;
    private readonly HistoricalDataService _historical;
    private readonly LocationService _location;

    public FloodRiskService(
        GeocodingService geocoding,
        RiskService riskService,
        WeatherService weather,
        MLPredictionService ml,
        HistoricalDataService historical,
        LocationService location)
    {
        _geocoding = geocoding;
        _riskService = riskService;
        _weather = weather;
        _ml = ml;
        _historical = historical;
        _location = location;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PUBLIC ENTRY POINT
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Assess flood risk. Address takes priority if both fields are provided.
    /// </summary>
    public async Task<FloodRiskServiceResult> AssessAsync(FloodRiskRequest request)
    {
        // ── Input validation ──────────────────────────────────────────────
        var validation = Validate(request);
        if (validation != null) return validation;

        bool useAddress = !string.IsNullOrWhiteSpace(request.Address);

        return useAddress
            ? await AssessFromAddressAsync(request.Address!.Trim())
            : await AssessFromDivisionAsync(request.Division!.Trim());
    }

    // ─────────────────────────────────────────────────────────────────────
    //  VALIDATION
    // ─────────────────────────────────────────────────────────────────────

    private static FloodRiskServiceResult? Validate(FloodRiskRequest request)
    {
        bool hasAddress  = !string.IsNullOrWhiteSpace(request.Address);
        bool hasDivision = !string.IsNullOrWhiteSpace(request.Division);

        if (!hasAddress && !hasDivision)
        {
            return Error(400, "MISSING_INPUT",
                "Either 'address' or 'division' must be provided.",
                "Both fields are missing or empty.",
                new
                {
                    example_address  = new { address  = "Colombo 07, Sri Lanka" },
                    example_division = new { division = "Kaduwela" }
                });
        }

        if (hasAddress && request.Address!.Trim().Length < 3)
        {
            return Error(400, "INVALID_ADDRESS",
                "Address is too short to be valid.",
                "Provide at least 3 characters.");
        }

        if (!hasAddress && hasDivision && request.Division!.Trim().Length < 2)
        {
            return Error(400, "INVALID_DIVISION",
                "Division name is too short to be valid.",
                "Provide a full Divisional Secretariat name (e.g. 'Kaduwela').");
        }

        return null; // valid
    }

    // ─────────────────────────────────────────────────────────────────────
    //  TYPE A — ADDRESS-BASED ASSESSMENT
    // ─────────────────────────────────────────────────────────────────────

    private async Task<FloodRiskServiceResult> AssessFromAddressAsync(string address)
    {
        // 1. Geocode
        GeocodeResult? geo;
        try   { geo = await _geocoding.GeocodeAsync(address); }
        catch { return Error(500, "GEOCODING_FAILED", "Geocoding service encountered an error."); }

        if (geo == null)
        {
            return Error(404, "LOCATION_NOT_FOUND",
                "The provided address could not be resolved.",
                $"'{address}' was not found. Ensure it is a valid Sri Lankan address.");
        }

        if (!GeocodingService.IsInSriLanka(geo.Lat, geo.Lon))
        {
            return Error(400, "OUT_OF_REGION",
                "The resolved location is outside Sri Lanka.",
                $"Resolved to ({geo.Lat}, {geo.Lon}).");
        }

        // Infer division for ML/historical lookups
        string? divisionName  = FindNearestDivision(geo.Lat, geo.Lon,
                                    out string? provinceName, out string? districtName);

        return await RunPipelineAsync(
            inputType:          "address",
            inputValue:         address,
            normalizedLocation: geo.DisplayName,
            lat:                geo.Lat,
            lng:                geo.Lon,
            provinceName:       provinceName,
            districtName:       districtName,
            divisionName:       divisionName);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  TYPE B — DIVISION-BASED ASSESSMENT
    // ─────────────────────────────────────────────────────────────────────

    private async Task<FloodRiskServiceResult> AssessFromDivisionAsync(string division)
    {
        // Search all provinces/districts for the division
        foreach (var province in _location.GetProvinces())
        {
            foreach (var district in _location.GetDistricts(province.Id))
            {
                var divList = _location.GetDivisions(district.Id);
                var matched = divList.FirstOrDefault(d =>
                    d.Name.Equals(division, StringComparison.OrdinalIgnoreCase));

                if (matched != null)
                {
                    return await RunPipelineAsync(
                        inputType:          "division",
                        inputValue:         division,
                        normalizedLocation: $"{matched.Name}, {district.Name}, {province.Name}, Sri Lanka",
                        lat:                matched.Coords.Lat,
                        lng:                matched.Coords.Lng,
                        provinceName:       province.Name,
                        districtName:       district.Name,
                        divisionName:       matched.Name);
                }
            }
        }

        // Not found — return helpful error with available divisions
        var allDivisions = _location.GetProvinces()
            .SelectMany(p => _location.GetDistricts(p.Id))
            .SelectMany(d => _location.GetDivisions(d.Id))
            .Select(d => d.Name)
            .OrderBy(n => n)
            .ToList();

        // Fuzzy suggestions: divisions containing the search term
        var suggestions = allDivisions
            .Where(n => n.Contains(division, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();

        return Error(404, "DIVISION_NOT_FOUND",
            $"Division '{division}' was not found in the flood risk database.",
            "Check the spelling or use GET /api/flood-risk/locations to list all valid divisions.",
            suggestions.Any() ? new { suggestions } : null);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  SHARED RISK PIPELINE
    // ─────────────────────────────────────────────────────────────────────

    private async Task<FloodRiskServiceResult> RunPipelineAsync(
        string inputType, string inputValue, string normalizedLocation,
        double lat, double lng,
        string? provinceName, string? districtName, string? divisionName)
    {
        // Step 1 — Weather / current rainfall
        double rainfall = 0;
        try
        {
            var weather = await _weather.GetCurrentWeatherAsync(lat, lng);
            rainfall = weather?.Rain ?? 0;
        }
        catch { /* Rainfall unavailable — assessment continues */ }

        // Step 2 — Geo-risk from flood zone polygons
        RiskResult riskResult;
        try
        {
            var point = new LatLng(lat, lng);
            riskResult = await _riskService.CalculateRiskAsync(point, rainfall);
        }
        catch
        {
            return Error(500, "RISK_CALCULATION_FAILED",
                "Flood risk data is currently unavailable.",
                "The geo-risk calculation service encountered an internal error.");
        }

        // Step 3 — ML model prediction
        MLDivisionPrediction? mlRaw = null;
        bool mlAvailable = false;
        try
        {
            if (!string.IsNullOrWhiteSpace(divisionName))
            {
                mlRaw = _ml.GetPredictionForDivision(divisionName);
                mlAvailable = mlRaw != null;
            }
        }
        catch { /* ML failure is non-fatal */ }

        // Step 4 — Sync geo-risk level with ML output
        if (mlRaw != null)
        {
            riskResult.Level = mlRaw.PredictedSeverity switch
            {
                "HIGH"   => RiskLevel.High,
                "MEDIUM" => RiskLevel.Medium,
                _        => RiskLevel.Low
            };
        }

        // Step 5 — Historical data
        var hist = new HistoricalRiskData();
        try
        {
            hist = _historical.GetHistoricalRisk(
                provinceName ?? "", districtName ?? "", divisionName ?? "");
        }
        catch { /* Non-fatal */ }

        // ── Map internal risk score (0–10) → external score (0.0–1.0) ──
        double externalScore = Math.Round(riskResult.Score / 10.0, 2);

        // ── Build ML result sub-object ───────────────────────────────────
        var mlResult = BuildMlResult(mlRaw, mlAvailable, hist.EventCount);

        // ── Build final response ─────────────────────────────────────────
        var response = new FloodRiskResponse
        {
            Success            = true,
            InputType          = inputType,
            InputValue         = inputValue,
            NormalizedLocation = normalizedLocation,
            FloodRiskLevel     = CapitalizeFirst(riskResult.Level.ToString()),
            RiskScore          = externalScore,
            RiskDescription    = BuildDescription(riskResult.Level, normalizedLocation),
            WasEscalated       = riskResult.WasEscalated,
            EscalationReason   = riskResult.EscalationReason,
            CurrentRainfallMm  = rainfall,
            MlModelResult      = mlResult,
            HistoricalFloodEvents   = hist.EventCount,
            HistoricalAvgAffected   = Math.Round(hist.AvgAffected, 1),
            Coordinates        = new LocationCoords { Latitude = lat, Longitude = lng },
            DataSource         = "Flood risk database + ML model prediction",
            Timestamp          = DateTime.UtcNow
        };

        return new FloodRiskServiceResult
        {
            Success       = true,
            Response      = response,
            HttpStatusCode = 200
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private MlModelResult BuildMlResult(
        MLDivisionPrediction? ml, bool available, int historicalEventCount)
    {
        if (!available || ml == null)
        {
            return new MlModelResult
            {
                Available       = false,
                PredictionLabel = "Not Available",
                ModelName       = "flood_risk_rf_model",
                ModelVersion    = "current"
            };
        }

        double score = ml.PredictedSeverity switch
        {
            "HIGH"   => 0.85,
            "MEDIUM" => 0.55,
            _        => 0.20
        };

        // Confidence: derived from historical data volume
        // More events → higher confidence (capped at 0.95)
        double confidence = historicalEventCount switch
        {
            >= 50 => 0.95,
            >= 20 => 0.87,
            >= 10 => 0.75,
            >= 5  => 0.65,
            _     => 0.50
        };

        return new MlModelResult
        {
            Available        = true,
            PredictionLabel  = $"{CapitalizeFirst(ml.PredictedSeverity.ToLower())} Flood Risk",
            PredictionScore  = score,
            Confidence       = confidence,
            PredictedAffected = ml.PredictedAffected,
            ModelName        = "flood_risk_rf_model",
            ModelVersion     = "current"
        };
    }

    private static string BuildDescription(RiskLevel level, string location) => level switch
    {
        RiskLevel.High    => $"This area has a HIGH flood risk. Immediate precautions are strongly advised.",
        RiskLevel.Medium  => $"This area has a medium flood risk based on available flood risk data.",
        RiskLevel.Low     => $"This area has a low flood risk. Standard safety measures are recommended.",
        _                 => $"Flood risk data for this area is currently unavailable."
    };

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..].ToLower();

    private string? FindNearestDivision(double lat, double lng,
        out string? provinceName, out string? districtName)
    {
        string? nearestDiv = null;
        provinceName = null;
        districtName = null;
        double nearestDist = double.MaxValue;

        foreach (var province in _location.GetProvinces())
        {
            foreach (var district in _location.GetDistricts(province.Id))
            {
                foreach (var division in _location.GetDivisions(district.Id))
                {
                    double dLat = division.Coords.Lat - lat;
                    double dLng = division.Coords.Lng - lng;
                    double dist = dLat * dLat + dLng * dLng;

                    if (dist < nearestDist)
                    {
                        nearestDist  = dist;
                        nearestDiv   = division.Name;
                        provinceName = province.Name;
                        districtName = district.Name;
                    }
                }
            }
        }

        return nearestDiv;
    }

    private static FloodRiskServiceResult Error(
        int statusCode, string code, string message,
        string? detail = null, object? options = null) =>
        new()
        {
            Success        = false,
            HttpStatusCode = statusCode,
            Error = new FloodRiskError
            {
                Success          = false,
                ErrorCode        = code,
                Message          = message,
                Detail           = detail,
                AvailableOptions = options,
                Timestamp        = DateTime.UtcNow
            }
        };
}
