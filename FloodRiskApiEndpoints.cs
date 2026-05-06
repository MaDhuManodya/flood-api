using FloodApp.Models;
using FloodApp.Services;
using Microsoft.OpenApi.Models;

namespace FloodApp;

/// <summary>
/// Clean, company-grade Flood Risk API endpoints.
///
/// Routes:
///   POST /api/flood-risk/address   — Assess by free-text address
///   POST /api/flood-risk/division  — Assess by Divisional Secretariat
///   GET  /api/flood-risk/locations — List all valid Province/District/Division values
///   GET  /api/flood-risk/health    — Health check
///
/// Swagger UI: GET /api-docs
/// </summary>
public static class FloodRiskApiEndpoints
{
    public static void MapFloodRiskApiEndpoints(this WebApplication app)
    {
        // ════════════════════════════════════════════════════════════════
        //  POST /api/flood-risk/address
        // ════════════════════════════════════════════════════════════════
        app.MapPost("/api/flood-risk/address", async (
            AddressRiskRequest request,
            FloodRiskApiService service,
            IWebHostEnvironment env) =>
        {
            try
            {
                var result = await service.AssessFromAddressAsync(request?.Address);
                return MapToResult(result, env.IsDevelopment());
            }
            catch (Exception ex)
            {
                return Results.Json(
                    InternalError(env.IsDevelopment() ? ex.Message : null),
                    statusCode: 500);
            }
        })
        .WithName("FloodRiskByAddress")
        .WithTags("Flood Risk")
        .WithSummary("Assess flood risk by address")
        .WithDescription(
            "Accepts a free-text Sri Lankan address string. " +
            "Geocodes the address, runs flood zone polygon matching, " +
            "ML model prediction, and historical data lookup. " +
            "Returns a structured flood risk assessment.")
        .Accepts<AddressRiskRequest>("application/json")
        .Produces<FloodRiskApiResponse>(200,  "application/json")
        .Produces<FloodRiskApiError>(400, "application/json")
        .Produces<FloodRiskApiError>(404, "application/json")
        .Produces<FloodRiskApiError>(422, "application/json")
        .Produces<FloodRiskApiError>(500, "application/json");

        // ════════════════════════════════════════════════════════════════
        //  POST /api/flood-risk/division
        // ════════════════════════════════════════════════════════════════
        app.MapPost("/api/flood-risk/division", async (
            DivisionRiskRequest request,
            FloodRiskApiService service,
            IWebHostEnvironment env) =>
        {
            try
            {
                var result = await service.AssessFromDivisionAsync(request?.Division);
                return MapToResult(result, env.IsDevelopment());
            }
            catch (Exception ex)
            {
                return Results.Json(
                    InternalError(env.IsDevelopment() ? ex.Message : null),
                    statusCode: 500);
            }
        })
        .WithName("FloodRiskByDivision")
        .WithTags("Flood Risk")
        .WithSummary("Assess flood risk by Divisional Secretariat")
        .WithDescription(
            "Accepts a Sri Lankan Divisional Secretariat name. " +
            "Looks it up in the location database, runs flood zone polygon matching, " +
            "ML model prediction, and historical data lookup. " +
            "Returns a structured flood risk assessment. " +
            "Use GET /api/flood-risk/locations to find all valid division names.")
        .Accepts<DivisionRiskRequest>("application/json")
        .Produces<FloodRiskApiResponse>(200,  "application/json")
        .Produces<FloodRiskApiError>(400, "application/json")
        .Produces<FloodRiskApiError>(404, "application/json")
        .Produces<FloodRiskApiError>(422, "application/json")
        .Produces<FloodRiskApiError>(500, "application/json");

        // ════════════════════════════════════════════════════════════════
        //  GET /api/flood-risk/locations
        // ════════════════════════════════════════════════════════════════
        app.MapGet("/api/flood-risk/locations", (LocationService locationService) =>
        {
            var data = locationService.GetProvinces().Select(p => new
            {
                province = p.Name,
                districts = locationService.GetDistricts(p.Id).Select(d => new
                {
                    district = d.Name,
                    divisional_secretariats = locationService
                        .GetDivisions(d.Id)
                        .Select(div => div.Name)
                        .ToList()
                }).ToList()
            }).ToList();

            return Results.Ok(new
            {
                success         = true,
                total_divisions = data.Sum(p => p.districts.Sum(d => d.divisional_secretariats.Count)),
                data,
                timestamp       = DateTime.UtcNow
            });
        })
        .WithName("FloodRiskLocations")
        .WithTags("Flood Risk")
        .WithSummary("List all valid locations")
        .WithDescription(
            "Returns the complete Province → District → Divisional Secretariat hierarchy. " +
            "Use this to discover valid values for the POST /api/flood-risk/division endpoint.");

        // ════════════════════════════════════════════════════════════════
        //  GET /api/flood-risk/health
        // ════════════════════════════════════════════════════════════════
        app.MapGet("/api/flood-risk/health", () => Results.Ok(new
        {
            status    = "healthy",
            service   = "SLIC Flood Risk API",
            version   = "1.0.0",
            endpoints = new[]
            {
                "POST /api/flood-risk/address",
                "POST /api/flood-risk/division",
                "GET  /api/flood-risk/locations"
            },
            swagger   = "/api-docs",
            timestamp = DateTime.UtcNow
        }))
        .WithName("FloodRiskHealth")
        .WithTags("Flood Risk")
        .WithSummary("Health check")
        .WithDescription("Returns the service health status and available endpoints.");
    }

    // ── Shared result mapper ─────────────────────────────────────────────
    private static IResult MapToResult(FloodRiskApiResult result, bool isDev) =>
        result.Success
            ? Results.Ok(result.Response)
            : result.HttpStatusCode switch
            {
                400 => Results.BadRequest(result.Error),
                404 => Results.NotFound(result.Error),
                422 => Results.UnprocessableEntity(result.Error),
                _   => Results.Json(result.Error, statusCode: result.HttpStatusCode)
            };

    private static FloodRiskApiError InternalError(string? detail) => new()
    {
        ErrorCode = "INTERNAL_SERVER_ERROR",
        Message   = "An unexpected error occurred while processing your request.",
        Detail    = detail,
        Timestamp = DateTime.UtcNow
    };
}
