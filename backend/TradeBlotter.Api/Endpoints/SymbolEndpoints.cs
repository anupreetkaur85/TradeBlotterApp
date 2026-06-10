using TradeBlotter.Application.Abstractions.Persistence;
using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Api.Endpoints;

public static class SymbolEndpoints
{
    public static void MapSymbolEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/symbols/search", SearchAsync)
            .WithTags("Symbols")
            .Produces<IReadOnlyList<SymbolResponse>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> SearchAsync(
        ISymbolRepository repository,
        CancellationToken ct,
        string? q = null,
        int? limit = null)
    {
        int take = Math.Clamp(limit ?? 8, 1, 25);
        IReadOnlyList<SymbolResponse> results = await repository.SearchAsync(q ?? string.Empty, take, ct);
        return Results.Ok(results);
    }
}
