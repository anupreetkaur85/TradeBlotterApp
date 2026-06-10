using Serilog;
using TradeBlotter.Api;
using TradeBlotter.Api.Endpoints;
using TradeBlotter.Api.Hubs;
using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Options;
using TradeBlotter.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.Console());

bool useInMemory = builder.Configuration.GetValue<bool>("Database:UseInMemory");

builder.Services
    .AddOptions<SeedingOptions>()
    .BindConfiguration(SeedingOptions.SectionName)
    .Validate(o => !o.RunOnStartup || o.Enabled, "Seeding:RunOnStartup requires Seeding:Enabled.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AdminOptions>()
    .BindConfiguration(AdminOptions.SectionName)
    .ValidateOnStart();

builder.Services
    .AddOptions<MigrationOptions>()
    .BindConfiguration(MigrationOptions.SectionName)
    .ValidateOnStart();

if (useInMemory)
{
    builder.Services.AddInMemoryPersistence();
}
else
{
    string connectionString = builder.Configuration.GetConnectionString("TradeBlotter")
        ?? throw new InvalidOperationException("Connection string 'TradeBlotter' is not configured.");
    builder.Services.AddInfrastructure(connectionString);
}

builder.Services.AddApplicationServices();

// Real-time push (server -> client) for the live blotter.
builder.Services.AddSignalR();
builder.Services.AddSingleton<SignalRTradeNotifier>();
builder.Services.AddSingleton<ITradeNotifier>(
    services => services.GetRequiredService<SignalRTradeNotifier>());
builder.Services.AddHostedService(
    services => services.GetRequiredService<SignalRTradeNotifier>());

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string corsPolicy = "spa";
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(corsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // required for the SignalR browser client

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    await Results.Problem(
            statusCode: statusCodeContext.HttpContext.Response.StatusCode)
        .ExecuteAsync(statusCodeContext.HttpContext);
});

// Apply migrations (and optional startup seed) before serving traffic.
await StartupTasks.RunAsync(app);

app.UseSerilogRequestLogging();
app.UseCors(corsPolicy);

if (app.Environment.IsDevelopment())
{
    // OpenAPI doc + Swagger UI both served under /api-docs.
    app.UseSwagger(options => options.RouteTemplate = "api-docs/{documentName}/swagger.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/api-docs/v1/swagger.json", "TradeBlotter API v1");
        options.RoutePrefix = "api-docs";
        options.DocumentTitle = "Trade Blotter API";
    });

    // Land the bare root on the API docs instead of a 404.
    app.MapGet("/", () => Results.Redirect("/api-docs")).ExcludeFromDescription();
}

app.MapHealthEndpoints();
app.MapTradeEndpoints();
app.MapPositionEndpoints();
app.MapSymbolEndpoints();
app.MapAdminEndpoints();
app.MapHub<BlotterHub>(BlotterHub.Path);

app.Run();

// Exposed so the integration test project can use WebApplicationFactory<Program>.
public partial class Program;
