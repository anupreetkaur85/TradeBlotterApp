using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Api.Endpoints;

public static class PositionEndpoints
{
    public static void MapPositionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/positions", GetAsync)
            .WithTags("Positions")
            .Produces<IReadOnlyList<PositionResponse>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetAsync(IPositionService service, CancellationToken ct)
    {
        IReadOnlyList<PositionResponse> positions = await service.GetPositionsAsync(ct);
        return Results.Ok(positions);
    }
}
