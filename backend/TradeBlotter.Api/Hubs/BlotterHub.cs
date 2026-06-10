using Microsoft.AspNetCore.SignalR;

namespace TradeBlotter.Api.Hubs;

/// <summary>
/// Server-to-client push channel for the live blotter. Clients only subscribe;
/// they never invoke hub methods (trades are submitted over REST), so the hub
/// has no callable surface. Event names are defined by <see cref="SignalRTradeNotifier"/>.
/// </summary>
public sealed class BlotterHub : Hub
{
    public const string Path = "/hubs/blotter";
}
