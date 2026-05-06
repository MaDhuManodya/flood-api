using FloodApp.Models;

namespace FloodApp.Services;

// ── Internal result carrier ──────────────────────────────────────────────────

public class FloodRiskApiResult
{
    public bool Success { get; set; }
    public FloodRiskApiResponse? Response { get; set; }
    public FloodRiskApiError? Error { get; set; }
    public int HttpStatusCode { get; set; } = 200;
}

// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Core business logic for the reusable Flood Risk API.
/// 
/// Encapsulates:
///  - Input validation
///  - Address geocoding (Type A)
///  - Division lookup (Type B)
///  - Geo-risk calculation (flood zone polygon matching)
///  - ML model prediction
///  - Historical flood data
///  - Structured response building
///
/// Used by both POST /api/flood-risk/address and POST /api/flood-risk/division.
/// Designed for reuse across multiple company platforms.
/// </summary>
public class FloodRiskApiService
{
    private readonly GeocodingService _geocoding;
    private readonly RiskService _riskService;
    private readonly WeatherService _weather;
    private readonly MLPredictionService _ml;
    private readonly HistoricalDataService _historical;
    private readonly LocationService _location;

    public FloodRiskApiService(
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

    // ═════════════════════════════════════════════════════════════════════
    //  PUBLIC ENTRY POINTS
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Assess flood risk from a free-text address string.
    /// Geocodes the address, validates it is within Sri Lanka,
    /// then runs the full risk assessment pipeline.
    /// </summary>
    public async Task<FloodRiskApiResult> AssessFromAddressAsync(string? address)
    {
        // ── Validation ──
        if (string.IsNullOrWhiteSpace(address))
            return Fail(400, "MISSING_ADDRESS",
                "The 'address' field is required.",
                "Provide a non-empty address string. Example: 'Colombo 07, Sri Lanka'");

        address = address.Trim();

        if (address.Length < 3)
            return Fail(400, "INVALID_ADDRESS",
                "Address is too short to be valid.",
                "Provide at least 3 characters for a meaningful address lookup.");

        // ── Geocoding ──
        GeocodeResult? geo;
        try   { geo = await _geocoding.GeocodeAsync(address); }
        catch { return Fail(500, "GEOCODING_ERROR", "Geocoding service encountered an internal error."); }

        if (geo == null)
            return Fail(404, "LOCATION_NOT_FOUND",
                "The provided address could not be resolved to coordinates.",
                $"'{address}' was not recognised. Ensure it is a valid Sri Lankan address.");

        if (!GeocodingService.IsInSriLanka(geo.Lat, geo.Lon))
            return Fail(400, "OUT_OF_REGION",
                "The resolved location is outside Sri Lanka.",
                $"Address resolved to ({geo.Lat:F4}, {geo.Lon:F4}) which is outside the supported region.");

        // Infer nearest division for ML/historical lookups
        var division = FindNearestDivision(geo.Lat, geo.Lon,
                           out var provinceName, out var districtName);

        return await RunPipelineAsync(
            inputType:         "address",
            inputValue:        address,
            normalizedLocation: geo.DisplayName,
            lat:               geo.Lat,
            lng:               geo.Lon,
            provinceName:      provinceName,
            districtName:      districtName,
            divisionName:      division);
    }

    /// <summary>
    /// Assess flood risk from a Divisional Secretariat name.
    /// Looks up the division in the location database,
    /// then runs the full risk assessment pipeline.
    /// </summary>
    public async Task<FloodRiskApiResult> AssessFromDivisionAsync(string? division)
    {
        // ── Validation ──
        if (string.IsNullOrWhiteSpace(division))
            return Fail(400, "MISSING_DIVISION",
                "The 'division' field is required.",
                "Provide a non-empty Divisional Secretariat name. Example: 'Kaduwela'");

        division = division.Trim();

        if (division.Length < 2)
            return Fail(400, "INVALID_DIVISION",
                "Division name is too short.",
                "Provide a full Divisional Secretariat name (e.g. 'Kaduwela', 'Colombo').");

        // ── Division lookup ──
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
                        inputType:         "division",
                        inputValue:        division,
                        normalizedLocation: $"{matched.Name}, {district.Name}, {province.Name}, Sri Lanka",
                        lat:               matched.Coords.Lat,
                        lng:               matched.Coords.Lng,
                        provinceName:      province.Name,
                        districtName:      district.Name,
                        divisionName:      matched.Name);
                }
            }
        }

        // Not found — include fuzzy suggestions
        var suggestions = _location.GetProvinces()
            .SelectMany(p => _location.GetDistricts(p.Id))
            .SelectMany(d => _location.GetDivisions(d.Id))
            .Select(d => d.Name)
            .Where(n => n.Contains(division, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n)
            .Take(10)
            .ToList();

        return Fail(404, "DIVISION_NOT_FOUND",
            $"Division '{division}' was not found in the flood risk database.",
            "Use GET /api/flood-risk/locations to list all valid Divisional Secretariat names.",
            suggestions.Any() ? new { did_you_mean = suggestions } : null);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  SHARED RISK PIPELINE
    // ═════════════════════════════════════════════════════════════════════

    private async Task<FloodRiskApiResult> RunPipelineAsync(
        string inputType, string inputValue, string normalizedLocation,
        double lat, double lng,
        string? provinceName, string? districtName, string? divisionName)
    {
        // ── Step 1: Current weather / rainfall ──
        double rainfall = 0;
        try
        {
            var weather = await _weather.GetCurrentWeatherAsync(lat, lng);
            rainfall = weather?.Rain ?? 0;
        }
        catch { /* Non-fatal: assessment continues without live rainfall */ }

        // ── Step 2: Geo-risk calculation from flood zone polygons ──
        RiskResult riskResult;
        try
        {
            riskResult = await _riskService.CalculateRiskAsync(new LatLng(lat, lng), rainfall);
        }
        catch
        {
            // 422 Unprocessable Entity: valid location, but risk data unavailable
            return Fail(422, "FLOOD_RISK_UNAVAILABLE",
                "Flood risk data is currently unavailable for this location.",
                "The geo-risk calculation service could not process this request.");
        }

        // ── Step 3: ML model prediction ──
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
        catch { /* Non-fatal: ML failure is handled gracefully */ }

        // ── Step 4: Sync final risk level with ML output ──
        if (mlRaw != null)
        {
            riskResult.Level = mlRaw.PredictedSeverity switch
            {
                "HIGH"   => RiskLevel.High,
                "MEDIUM" => RiskLevel.Medium,
                _        => RiskLevel.Low
            };
        }

        // ── Step 5: Historical flood data ──
        HistoricalRiskData hist = new();
        try
        {
            hist = _historical.GetHistoricalRisk(
                provinceName ?? "", districtName ?? "", divisionName ?? "");
        }
        catch { /* Non-fatal */ }

        // ── Build ML result sub-object ──
        var mlResult = BuildMlResult(mlRaw, mlAvailable, hist.EventCount);

        // ── Map internal score (0–10) to external score (0.0–1.0) ──
        double externalScore = Math.Round(riskResult.Score / 10.0, 2);

        var response = new FloodRiskApiResponse
        {
            Success            = true,
            InputType          = inputType,
            InputValue         = inputValue,
            NormalizedLocation = normalizedLocation,
            Latitude           = lat,
            Longitude          = lng,
            FloodRiskLevel     = Capitalize(riskResult.Level.ToString()),
            RiskScore          = externalScore,
            RiskDescription    = BuildDescription(riskResult.Level),
            WasEscalated       = riskResult.WasEscalated,
            EscalationReason   = riskResult.EscalationReason,
            CurrentRainfallMm  = rainfall,
            MlModelResult      = mlResult,
            HistoricalFloodEvents  = hist.EventCount,
            HistoricalAvgAffected  = Math.Round(hist.AvgAffected, 1),
            DataSource         = "Flood risk database + ML model prediction",
            Timestamp          = DateTime.UtcNow
        };

        return new FloodRiskApiResult { Success = true, Response = response, HttpStatusCode = 200 };
    }

    // ═════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════════════════════

    private static FloodRiskMlResult BuildMlResult(
        MLDivisionPrediction? ml, bool available, int historicalEvents)
    {
        if (!available || ml == null)
            return new FloodRiskMlResult { Available = false };

        double score = ml.PredictedSeverity switch
        {
            "HIGH"   => 0.85,
            "MEDIUM" => 0.55,
            _        => 0.20
        };

        // Confidence derived from historical data volume
        double confidence = historicalEvents switch
        {
            >= 50 => 0.95,
            >= 20 => 0.87,
            >= 10 => 0.75,
            >= 5  => 0.65,
            _     => 0.50
        };

        return new FloodRiskMlResult
        {
            Available        = true,
            PredictionLabel  = $"{Capitalize(ml.PredictedSeverity.ToLower())} Flood Risk",
            PredictionScore  = score,
            Confidence       = confidence,
            PredictedAffected = ml.PredictedAffected > 0 ? ml.PredictedAffected : null,
            ModelName        = "flood_risk_rf_model",
            ModelVersion     = "current"
        };
    }

    private static string BuildDescription(RiskLevel level) => level switch
    {
        RiskLevel.High   => "This area has a HIGH flood risk. Immediate precautions and evacuation plans are strongly advised.",
        RiskLevel.Medium => "This area has a medium flood risk based on available flood risk data.",
        RiskLevel.Low    => "This area has a low flood risk. Standard safety measures are recommended.",
        _                => "Flood risk data for this area is currently unavailable or insufficient."
    };

    private static string Capitalize(string s) =>
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
                foreach (var div in _location.GetDivisions(district.Id))
                {
                    double d = Math.Pow(div.Coords.Lat - lat, 2) + Math.Pow(div.Coords.Lng - lng, 2);
                    if (d < nearestDist)
                    {
                        nearestDist  = d;
                        nearestDiv   = div.Name;
                        provinceName = province.Name;
                        districtName = district.Name;
                    }
                }
            }
        }
        return nearestDiv;
    }

    private static FloodRiskApiResult Fail(int status, string code, string message,
        string? detail = null, object? options = null) =>
        new()
        {
            Success        = false,
            HttpStatusCode = status,
            Error = new FloodRiskApiError
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
