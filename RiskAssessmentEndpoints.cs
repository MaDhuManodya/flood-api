using FloodApp.Models;
using FloodApp.Services;

namespace FloodApp;

public static class RiskAssessmentEndpoints
{
    public static void MapRiskAssessmentEndpoints(this WebApplication app)
    {
        // ── POST /api/risk/assess ──────────────────────────────────────────
        // Accepts JSON body with either:
        //   { "address": "Colombo Fort" }                          → Direct Address
        //   { "province": "Western", "district": "Colombo",
        //     "division": "Kaduwela" }                             → Hierarchical
        app.MapPost("/api/risk/assess", async (
            RiskAssessmentRequest request,
            RiskService riskService,
            WeatherService weatherService,
            MLPredictionService mlPrediction,
            HistoricalDataService historicalData,
            GeocodingService geocodingService,
            LocationService locationService) =>
        {
            // ── Determine input method ──────────────────────────────────
            bool hasAddress = !string.IsNullOrWhiteSpace(request.Address);
            bool hasHierarchy = !string.IsNullOrWhiteSpace(request.Province)
                             || !string.IsNullOrWhiteSpace(request.District)
                             || !string.IsNullOrWhiteSpace(request.Division);

            if (!hasAddress && !hasHierarchy)
            {
                return Results.BadRequest(new
                {
                    error = "Please provide either an 'address' string, or 'province'/'district'/'division' fields.",
                    examples = new
                    {
                        addressMethod = new { address = "Colombo Fort" },
                        hierarchicalMethod = new { province = "Western", district = "Colombo", division = "Kaduwela" }
                    }
                });
            }

            // Variables we need to resolve
            double lat = 0, lng = 0;
            string resolvedLocation = "";
            string inputMethod;
            string? divisionName = null;
            string? provinceName = null;
            string? districtName = null;

            // ── Method 1: Direct Address Input ──────────────────────────
            if (hasAddress)
            {
                inputMethod = "address";

                var geocodeResult = await geocodingService.GeocodeAsync(request.Address!);
                if (geocodeResult == null)
                {
                    return Results.NotFound(new
                    {
                        error = "Could not geocode the provided address. Please check the address and try again.",
                        address = request.Address,
                        hint = "The address must be within Sri Lanka."
                    });
                }

                lat = geocodeResult.Lat;
                lng = geocodeResult.Lon;
                resolvedLocation = geocodeResult.DisplayName;

                // Validate Sri Lanka bounds
                if (!GeocodingService.IsInSriLanka(lat, lng))
                {
                    return Results.BadRequest(new
                    {
                        error = "The resolved location is outside Sri Lanka.",
                        resolvedAddress = resolvedLocation,
                        lat,
                        lng
                    });
                }

                // Try to infer division from geocoded location for ML/historical lookups
                // by finding the nearest division in the location service
                divisionName = FindNearestDivisionName(locationService, lat, lng);
                if (divisionName != null)
                {
                    var divInfo = FindDivisionHierarchy(locationService, divisionName);
                    provinceName = divInfo.Province;
                    districtName = divInfo.District;
                }
            }
            // ── Method 2: Hierarchical Selection ────────────────────────
            else
            {
                inputMethod = "hierarchical";
                provinceName = request.Province?.Trim();
                districtName = request.District?.Trim();
                divisionName = request.Division?.Trim();

                // Validate the hierarchy
                var provinces = locationService.GetProvinces();

                // Province lookup
                Province? matchedProvince = null;
                if (!string.IsNullOrWhiteSpace(provinceName))
                {
                    matchedProvince = provinces.FirstOrDefault(p =>
                        p.Name.Equals(provinceName, StringComparison.OrdinalIgnoreCase));

                    if (matchedProvince == null)
                    {
                        return Results.BadRequest(new
                        {
                            error = $"Province '{provinceName}' not found.",
                            availableProvinces = provinces.Select(p => p.Name).ToList()
                        });
                    }
                }

                // District lookup
                District? matchedDistrict = null;
                if (!string.IsNullOrWhiteSpace(districtName))
                {
                    if (matchedProvince == null)
                    {
                        return Results.BadRequest(new
                        {
                            error = "Please provide a province when specifying a district."
                        });
                    }

                    var districts = locationService.GetDistricts(matchedProvince.Id);
                    matchedDistrict = districts.FirstOrDefault(d =>
                        d.Name.Equals(districtName, StringComparison.OrdinalIgnoreCase));

                    if (matchedDistrict == null)
                    {
                        return Results.BadRequest(new
                        {
                            error = $"District '{districtName}' not found in province '{matchedProvince.Name}'.",
                            availableDistricts = districts.Select(d => d.Name).ToList()
                        });
                    }
                }

                // Division lookup
                Division? matchedDivision = null;
                if (!string.IsNullOrWhiteSpace(divisionName))
                {
                    if (matchedDistrict == null)
                    {
                        return Results.BadRequest(new
                        {
                            error = "Please provide a province and district when specifying a division."
                        });
                    }

                    var divisions = locationService.GetDivisions(matchedDistrict.Id);
                    matchedDivision = divisions.FirstOrDefault(d =>
                        d.Name.Equals(divisionName, StringComparison.OrdinalIgnoreCase));

                    if (matchedDivision == null)
                    {
                        return Results.BadRequest(new
                        {
                            error = $"Division '{divisionName}' not found in district '{matchedDistrict.Name}'.",
                            availableDivisions = divisions.Select(d => d.Name).ToList()
                        });
                    }
                }

                // Resolve coordinates from the most specific match
                if (matchedDivision != null)
                {
                    lat = matchedDivision.Coords.Lat;
                    lng = matchedDivision.Coords.Lng;
                    resolvedLocation = $"{matchedDivision.Name}, {matchedDistrict!.Name}, {matchedProvince!.Name}";
                }
                else if (matchedDistrict != null)
                {
                    lat = matchedDistrict.Coords.Lat;
                    lng = matchedDistrict.Coords.Lng;
                    resolvedLocation = $"{matchedDistrict.Name}, {matchedProvince!.Name}";
                }
                else if (matchedProvince != null)
                {
                    lat = matchedProvince.Coords.Lat;
                    lng = matchedProvince.Coords.Lng;
                    resolvedLocation = matchedProvince.Name;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            //  SHARED RISK ASSESSMENT PIPELINE
            // ═══════════════════════════════════════════════════════════════

            // 1. Get current weather
            double currentRainfall = 0;
            try
            {
                var weather = await weatherService.GetCurrentWeatherAsync(lat, lng);
                currentRainfall = weather?.Rain ?? 0;
            }
            catch { /* Weather API failure should not block assessment */ }

            // 2. Calculate geo-risk from risk zones
            var point = new LatLng(lat, lng);
            var riskResult = await riskService.CalculateRiskAsync(point, currentRainfall);

            // 3. Get ML prediction for division
            MLDivisionPrediction? mlPred = null;
            if (!string.IsNullOrWhiteSpace(divisionName))
            {
                mlPred = mlPrediction.GetPredictionForDivision(divisionName);
            }

            // 4. Get historical data
            var histData = historicalData.GetHistoricalRisk(
                provinceName ?? "", districtName ?? "", divisionName ?? "");

            // 5. Sync risk level with ML prediction (same logic as existing endpoint)
            if (mlPred != null)
            {
                string mlSeverity = mlPred.PredictedSeverity;
                if (mlSeverity == "HIGH") riskResult.Level = RiskLevel.High;
                else if (mlSeverity == "MEDIUM") riskResult.Level = RiskLevel.Medium;
                else riskResult.Level = RiskLevel.Low;
            }

            // 6. Build comprehensive response
            var response = new RiskAssessmentResponse
            {
                InputMethod = inputMethod,
                ResolvedLocation = resolvedLocation,
                Latitude = lat,
                Longitude = lng,

                RiskLevel = riskResult.Level.ToString().ToUpper(),
                RiskScore = riskResult.Score,
                Message = riskResult.Message,
                ColorCode = riskResult.ColorCode,
                WasEscalated = riskResult.WasEscalated,
                EscalationReason = riskResult.EscalationReason,

                CurrentRainfallMm = currentRainfall,

                MlPredictedSeverity = mlPred?.PredictedSeverity,
                MlPredictedAffected = mlPred?.PredictedAffected,

                HistoricalEventCount = histData.EventCount,
                HistoricalAvgAffected = histData.AvgAffected,
                HistoricalAvgSeverity = histData.AvgSeverity,
                HistoricalAvgHousesDamaged = histData.AvgHousesDamaged,

                Timestamp = DateTime.UtcNow
            };

            return Results.Ok(response);
        })
        .WithName("AssessRisk")
        .WithTags("Risk Assessment")
        .Produces<RiskAssessmentResponse>(200)
        .Produces(400)
        .Produces(404);

        // ── GET /api/risk/locations ────────────────────────────────────────
        // Returns the full Province → District → Division hierarchy
        // so clients can populate cascading dropdowns.
        app.MapGet("/api/risk/locations", (LocationService locationService) =>
        {
            var provinces = locationService.GetProvinces();
            var hierarchy = provinces.Select(p => new
            {
                p.Name,
                Districts = locationService.GetDistricts(p.Id).Select(d => new
                {
                    d.Name,
                    Divisions = locationService.GetDivisions(d.Id).Select(div => div.Name).ToList()
                }).ToList()
            }).ToList();

            return Results.Ok(hierarchy);
        })
        .WithName("GetLocationHierarchy")
        .WithTags("Risk Assessment");
    }

    // ── Helper: Find nearest division by lat/lng ────────────────────────
    private static string? FindNearestDivisionName(LocationService locationService, double lat, double lng)
    {
        string? nearestName = null;
        double nearestDist = double.MaxValue;

        foreach (var province in locationService.GetProvinces())
        {
            foreach (var district in locationService.GetDistricts(province.Id))
            {
                foreach (var division in locationService.GetDivisions(district.Id))
                {
                    double dLat = division.Coords.Lat - lat;
                    double dLng = division.Coords.Lng - lng;
                    double dist = dLat * dLat + dLng * dLng; // squared distance is fine for comparison

                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestName = division.Name;
                    }
                }
            }
        }

        return nearestName;
    }

    // ── Helper: Find province/district for a division name ──────────────
    private static (string? Province, string? District) FindDivisionHierarchy(
        LocationService locationService, string divisionName)
    {
        foreach (var province in locationService.GetProvinces())
        {
            foreach (var district in locationService.GetDistricts(province.Id))
            {
                var divisions = locationService.GetDivisions(district.Id);
                if (divisions.Any(d => d.Name.Equals(divisionName, StringComparison.OrdinalIgnoreCase)))
                {
                    return (province.Name, district.Name);
                }
            }
        }
        return (null, null);
    }
}
