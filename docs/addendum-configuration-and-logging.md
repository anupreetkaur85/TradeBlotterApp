# Addendum: Database Initialization Configuration and Logging

Status: additive guidance for the backend scaffold already in progress.

This addendum adopts a configuration grouping and structured startup logging
pattern for database initialization. It does not change the trade schema, seed
API, Dapper repositories, DbUp migrations, or public contracts.

## Current scaffold alignment

The backend already has the required boundaries:

- `Migrations:RunOnStartup`
- `Seeding:Enabled`
- `Seeding:RunOnStartup`
- `Admin:Enabled`
- `StartupTasks.RunAsync`
- One shared `ISeedService` used by startup and admin endpoints
- DbUp migration failures that throw and prevent startup
- Serilog configuration and request logging

Keep these section names to avoid churn while scaffolding is underway.

## Configuration contract

Base configuration should remain conservative:

```json
{
  "Migrations": {
    "RunOnStartup": true
  },
  "Seeding": {
    "Enabled": false,
    "RunOnStartup": false
  },
  "Admin": {
    "Enabled": false
  }
}
```

Development overrides may enable the administrative surface without
automatically modifying data:

```json
{
  "Seeding": {
    "Enabled": true,
    "RunOnStartup": false
  },
  "Admin": {
    "Enabled": true
  }
}
```

Semantics:

- `Migrations:RunOnStartup` controls only DbUp execution.
- `Seeding:Enabled` is the master permission for all seed mutations.
- `Seeding:RunOnStartup` requests seed enablement during startup and is honored
  only when `Seeding:Enabled` is also true.
- `Admin:Enabled` controls whether `/admin/*` routes are mapped.
- Mapping admin routes does not override `Seeding:Enabled`.
- `GET /admin/seed-data` may report status when admin routes are enabled.
- `PUT /admin/seed-data` must reject mutation when `Seeding:Enabled` is false.

The current scaffold already enforces the startup condition. Add the same
master-switch check to the admin mutation path so configuration behavior is
consistent.

## Options binding and validation

Continue using typed options and add startup validation:

```csharp
builder.Services
    .AddOptions<SeedingOptions>()
    .BindConfiguration(SeedingOptions.SectionName)
    .Validate(
        options => !options.RunOnStartup || options.Enabled,
        "Seeding:RunOnStartup requires Seeding:Enabled.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AdminOptions>()
    .BindConfiguration(AdminOptions.SectionName)
    .ValidateOnStart();

builder.Services
    .AddOptions<MigrationOptions>()
    .BindConfiguration(MigrationOptions.SectionName)
    .ValidateOnStart();
```

This can replace the existing `Configure<T>` registrations when convenient. It
does not require changing option classes or consumers.

## Startup logging pattern

Emit one structured decision event before startup tasks:

```csharp
logger.LogInformation(
    "Database startup configuration: migrate={MigrateOnStartup}, " +
    "seedEnabled={SeedEnabled}, seedOnStartup={SeedOnStartup}, " +
    "adminEnabled={AdminEnabled}, environment={Environment}",
    migration.RunOnStartup,
    seeding.Enabled,
    seeding.RunOnStartup,
    admin.Enabled,
    app.Environment.EnvironmentName);
```

Then log operation boundaries and results:

```csharp
logger.LogInformation("Database migration starting");
// migrate
logger.LogInformation(
    "Database migration completed in {ElapsedMilliseconds} ms",
    stopwatch.ElapsedMilliseconds);

logger.LogInformation("Startup seed enable requested");
SeedStatus status = await seedService.SetEnabledAsync(true, ct);
logger.LogInformation(
    "Startup seed enable completed: enabled={SeedEnabled}, seedRowCount={SeedRowCount}",
    status.Enabled,
    status.SeedRowCount);
```

Recommended event coverage:

| Event | Level | Required properties |
|---|---|---|
| Startup configuration decision | Information | migration, seed, admin flags, environment |
| Migration start/completion | Information | elapsed time |
| Migration failure | Error | elapsed time, exception |
| Startup seed skipped | Information | reason |
| Seed enable/disable completion | Information | requested state, resulting count, elapsed time |
| Seed mutation denied | Warning | requested state, environment/config reason |

Use named properties rather than interpolated strings so Serilog preserves
structured fields.

## Failure behavior

Migration failure is fatal:

```csharp
try
{
    migrator.Migrate();
}
catch (Exception exception)
{
    logger.LogCritical(exception, "Database migration failed");
    throw;
}
```

Do not log an initialization failure and then allow the API to continue: serving
traffic against an unknown schema is unsafe.

Startup seed failure should also fail startup when
`Seeding:RunOnStartup = true`. The configuration explicitly requested a
database state that was not achieved. Interactive admin seed failures remain
normal request failures handled by ProblemDetails.

## Seed operation logging

Keep `ISeedService` as the single seed entry point. Add logging either in the
service or through a decorator, not independently in startup and endpoints:

```csharp
public async Task<SeedStatus> SetEnabledAsync(
    bool enabled,
    CancellationToken cancellationToken)
{
    var started = Stopwatch.GetTimestamp();

    SeedStatus status = enabled
        ? await EnableAsync(cancellationToken)
        : await DisableAsync(cancellationToken);

    logger.LogInformation(
        "Seed data state changed: requestedEnabled={RequestedEnabled}, " +
        "enabled={SeedEnabled}, seedRowCount={SeedRowCount}, elapsedMs={ElapsedMs}",
        enabled,
        status.Enabled,
        status.SeedRowCount,
        Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    return status;
}
```

This ensures startup and admin requests produce the same operational event.
Do not log every seed row or SQL parameter.

## Admin mutation guard

Preserve the existing endpoint and response contract. Add a small guard around
`PUT /admin/seed-data`:

```csharp
if (!seedingOptions.Value.Enabled)
{
    return Results.Problem(
        title: "Seed data mutation is disabled.",
        statusCode: StatusCodes.Status409Conflict);
}
```

Document `409 Conflict` in Swagger. This guard is configuration enforcement,
not authentication. Production should still leave `Admin:Enabled` false until
an authorization policy exists.

## Migration logging integration

DbUp currently writes directly to console. Prefer routing migration messages
through the application logger or keeping DbUp output concise while the
surrounding start/completion/failure events remain authoritative.

Do not log:

- Connection strings
- Database credentials
- Full SQL parameter values
- Request bodies
- Per-row seed details

## Incremental adoption checklist

These changes can be applied independently:

1. Keep current configuration section names and defaults.
2. Add the startup configuration decision log.
3. Add migration and startup-seed timing/result logs.
4. Enforce `Seeding:Enabled` in the admin `PUT` endpoint.
5. Add `ValidateOnStart` option binding when convenient.
6. Centralize seed completion logging in `SeedService` or a decorator.
7. Add tests for invalid option combinations and disabled seed mutation.

No database migration, endpoint rename, or frontend change is required.

## Tests to add

- Startup fails validation when `RunOnStartup=true` and `Enabled=false`.
- Startup does not call the seed service when startup seeding is disabled.
- Startup calls the same seed service used by the admin endpoint.
- `PUT /admin/seed-data` returns `409` when seed mutation is disabled.
- Migration exceptions propagate and prevent host startup.
- Seed enable/disable completion logs contain requested state and resulting
  seed count.

