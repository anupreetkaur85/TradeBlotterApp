using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Contracts.Requests;
using TradeBlotter.Application.Contracts.Responses;
using TradeBlotter.Application.Contracts.Results;
using TradeBlotter.Application.Models;
using TradeBlotter.Application.Validation;

namespace TradeBlotter.Api.Endpoints;

public static class TradeEndpoints
{
    public static void MapTradeEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/trades").WithTags("Trades");

        group.MapPost("/", CreateAsync)
            .Produces<TradeResponse>(StatusCodes.Status201Created)
            .Produces<TradeResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/", GetAsync)
            .Produces<IReadOnlyList<TradeResponse>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> CreateAsync(
        CreateTradeRequest request,
        ITradeService service,
        ITradeNotifier notifier,
        HttpContext http,
        CancellationToken ct)
    {
        if (!TradeValidator.TryValidate(request, out NormalizedTrade normalized, out Dictionary<string, string[]> errors))
        {
            return Results.ValidationProblem(errors);
        }

        if (!TryReadIdempotencyKey(http, out Guid? idempotencyKey))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["Idempotency-Key must be a valid GUID."],
            });
        }

        CreateTradeResult result = await service.CreateAsync(normalized, idempotencyKey, ct);

        if (result.Created)
        {
            notifier.TradeCreated(result.Trade);
        }

        return result.Created
            ? Results.Json(result.Trade, statusCode: StatusCodes.Status201Created)
            : Results.Ok(result.Trade);
    }

    private static async Task<IResult> GetAsync(
        ITradeService service,
        CancellationToken ct)
    {
        IReadOnlyList<TradeResponse> trades = await service.GetTradesAsync(ct);
        return Results.Ok(trades);
    }

    private static bool TryReadIdempotencyKey(HttpContext http, out Guid? key)
    {
        key = null;
        if (!http.Request.Headers.TryGetValue(
                "Idempotency-Key",
                out Microsoft.Extensions.Primitives.StringValues values))
        {
            return true;
        }

        if (!Guid.TryParse(values.ToString(), out Guid parsed))
        {
            return false;
        }

        key = parsed;
        return true;
    }
}
