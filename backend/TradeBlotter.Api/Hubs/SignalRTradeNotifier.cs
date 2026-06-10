using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using TradeBlotter.Application.Abstractions.Services;
using TradeBlotter.Application.Contracts.Responses;

namespace TradeBlotter.Api.Hubs;

/// <summary>
/// Bounded in-process notification queue and SignalR publisher. Request handlers
/// enqueue without waiting for network delivery; a background loop broadcasts
/// committed changes to connected clients.
/// </summary>
public sealed class SignalRTradeNotifier : BackgroundService, ITradeNotifier
{
    private const int QueueCapacity = 256;
    public const string TradeCreatedEvent = "TradeCreated";
    public const string BlotterChangedEvent = "BlotterChanged";

    private readonly Channel<RealtimeEvent> _channel;
    private readonly IHubContext<BlotterHub> _hub;
    private readonly ILogger<SignalRTradeNotifier> _logger;
    private int _reconciliationRequired;

    public SignalRTradeNotifier(IHubContext<BlotterHub> hub, ILogger<SignalRTradeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
        _channel = Channel.CreateBounded<RealtimeEvent>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public void TradeCreated(TradeResponse trade) =>
        Enqueue(new RealtimeEvent(RealtimeEventKind.TradeCreated, trade));

    public void BlotterChanged() =>
        Enqueue(new RealtimeEvent(RealtimeEventKind.BlotterChanged, Trade: null));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (RealtimeEvent realtimeEvent in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (realtimeEvent.Kind == RealtimeEventKind.TradeCreated)
                {
                    await _hub.Clients.All.SendAsync(
                        TradeCreatedEvent,
                        realtimeEvent.Trade!,
                        stoppingToken);
                }
                else
                {
                    await _hub.Clients.All.SendCoreAsync(
                        BlotterChangedEvent,
                        Array.Empty<object?>(),
                        stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to broadcast realtime event {RealtimeEventKind}",
                    realtimeEvent.Kind);
            }

            if (Interlocked.Exchange(ref _reconciliationRequired, 0) == 1)
            {
                await BroadcastBlotterChangedAsync(stoppingToken);
            }
        }
    }

    private void Enqueue(RealtimeEvent realtimeEvent)
    {
        if (!_channel.Writer.TryWrite(realtimeEvent))
        {
            Interlocked.Exchange(ref _reconciliationRequired, 1);
            _logger.LogWarning(
                "Realtime event queue is full; coalescing dropped event {RealtimeEventKind} into reconciliation",
                realtimeEvent.Kind);
        }
    }

    private async Task BroadcastBlotterChangedAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _hub.Clients.All.SendCoreAsync(
                BlotterChangedEvent,
                Array.Empty<object?>(),
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to broadcast realtime reconciliation event");
        }
    }

    private sealed record RealtimeEvent(RealtimeEventKind Kind, TradeResponse? Trade);

    private enum RealtimeEventKind
    {
        TradeCreated,
        BlotterChanged,
    }
}
