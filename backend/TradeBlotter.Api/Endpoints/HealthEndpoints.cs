using Microsoft.Extensions.DependencyInjection;
using TradeBlotter.Application.Abstractions.Persistence;

namespace TradeBlotter.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder health = app.MapGroup("/health").WithTags("Health");

        health.MapGet("/live", () => Results.Ok(new { status = "ok" }));
        health.MapGet("/ready", ReadyAsync)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithTags("Health");
    }

    private static async Task<IResult> ReadyAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // In-memory provider has no SQL connection factory — always ready.
        ISqlConnectionFactory? connectionFactory = services.GetService<ISqlConnectionFactory>();
        if (connectionFactory is null)
        {
            return Results.Ok(new { status = "ready" });
        }

        try
        {
            await using var connection =
                await connectionFactory.OpenAsync(cancellationToken);
            return Results.Ok(new { status = "ready" });
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException
            || !cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(
                title: "Database is unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
