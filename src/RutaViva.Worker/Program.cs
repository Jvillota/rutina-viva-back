using RutaViva.Application;
using RutaViva.Infrastructure;
using RutaViva.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOptions<WorkerSettings>()
    .Bind(builder.Configuration.GetSection(WorkerSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// The worker never migrates: the API owns the schema. Both loops tolerate a database that is not ready yet.
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<DailyCheckScheduler>();

await builder.Build().RunAsync();
