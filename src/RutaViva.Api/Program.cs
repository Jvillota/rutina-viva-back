using System.Text.Json.Serialization;
using Asp.Versioning;
using RutaViva.Api.ErrorHandling;
using RutaViva.Application;
using RutaViva.Infrastructure;
using RutaViva.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.ReportApiVersions = true;
        o.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    })
    .AddOpenApi();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// CORS only for configured origins (in Docker the UI goes through the nginx proxy and does not need it).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

app.MapOpenApi().WithDocumentPerVersion();
app.MapScalarApiReference(o => o.WithTitle("RutaViva API"));
app.MapHealthChecks("/health");
app.MapControllers();

await app.RunAsync();

/// <summary>Entry point, public so integration tests can host it with WebApplicationFactory.</summary>
public partial class Program;
