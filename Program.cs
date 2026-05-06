using FloodApp;
using FloodApp.Components;
using FloodApp.Services;
using FloodApp.State;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Load .env file if it exists
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var parts = line.Split('=', 2);
        if (parts.Length != 2) continue;
        Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
    }
}

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "SLIC Flood Risk API",
        Description = "Reusable flood risk assessment API for SLIC company platforms. " +
                      "Supports risk lookup by address or Divisional Secretariat, " +
                      "with ML model prediction and historical flood data.",
        Version     = "v1",
        Contact     = new OpenApiContact { Name = "SLIC Engineering" }
    });
});

// ── CORS ───────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<LocationService>();
builder.Services.AddSingleton<AdminService>();
builder.Services.AddSingleton<HistoricalFloodEventService>();
builder.Services.AddSingleton<AgentLocatorService>();
builder.Services.AddScoped<AppState>();
builder.Services.AddSingleton<GeoJsonService>();
builder.Services.AddSingleton<RiskService>();
builder.Services.AddSingleton<ShelterService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddSingleton<HistoricalDataService>();
builder.Services.AddSingleton<MLPredictionService>();

builder.Services.AddHttpClient<GeocodingService>();

// Reusable company-grade flood risk business logic services
builder.Services.AddScoped<FloodRiskService>();
builder.Services.AddScoped<FloodRiskApiService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

// ── Swagger UI at /api-docs ────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SLIC Flood Risk API v1");
    c.RoutePrefix = "api-docs";   // → available at GET /api-docs
    c.DocumentTitle = "SLIC Flood Risk API";
    c.DisplayRequestDuration();
});

app.UseStaticFiles();
app.UseAntiforgery();

app.MapFloodCheckEndpoints();
app.MapRiskAssessmentEndpoints();
app.MapFloodRiskApiEndpoints();  // ← POST /api/flood-risk/address & /division

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
