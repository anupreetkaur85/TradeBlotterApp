using Microsoft.Extensions.Options;
using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Models;
using TradeBlotter.Application.Options;

namespace TradeBlotter.Api.Endpoints;

/// <summary>
/// Development-only seed administration. Mapped only when <c>Admin:Enabled</c> is
/// true. Enabling inserts deterministic seed rows; disabling removes only seed
/// rows and never user-entered trades. Mutation additionally requires
/// <c>Seeding:Enabled</c>; otherwise it returns 409 Conflict.
/// </summary>
public static class AdminEndpoints
{
    public sealed record SeedToggleRequest(bool Enabled);

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        AdminOptions options = app.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
        if (!options.Enabled)
        {
            return;
        }

        RouteGroupBuilder group = app.MapGroup("/admin").WithTags("Admin");

        group.MapGet("/seed-data", GetStatusAsync)
            .Produces<SeedStatus>(StatusCodes.Status200OK);

        group.MapPut("/seed-data", SetAsync)
            .Produces<SeedStatus>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetStatusAsync(ISeedService service, CancellationToken ct)
    {
        SeedStatus status = await service.GetStatusAsync(ct);
        return Results.Ok(status);
    }

    private static async Task<IResult> SetAsync(
        SeedToggleRequest request,
        ISeedService service,
        IOptions<SeedingOptions> seeding,
        ILogger<SeedToggleRequest> logger,
        CancellationToken ct)
    {
        // Master-switch enforcement is configuration policy, not authentication.
        if (!seeding.Value.Enabled)
        {
            logger.LogWarning(
                "Seed mutation denied: requestedEnabled={RequestedEnabled}, reason=SeedingDisabled",
                request.Enabled);

            return Results.Problem(
                title: "Seed data mutation is disabled.",
                detail: "Set Seeding:Enabled to true to allow seed mutations.",
                statusCode: StatusCodes.Status409Conflict);
        }

        SeedStatus status = await service.SetEnabledAsync(request.Enabled, ct);
        return Results.Ok(status);
    }
}
