using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.FilmberSync.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FilmberSync.Services;

/// <summary>
/// Result of one durable delivery attempt.
/// </summary>
public enum EventDeliveryOutcome
{
    /// <summary>The event was accepted and may be removed.</summary>
    Delivered,

    /// <summary>The event should remain queued for a later attempt.</summary>
    Retry,

    /// <summary>The event cannot be delivered and may be removed.</summary>
    Discard
}

/// <summary>
/// Sends one durable event to Filmber.
/// </summary>
public interface IEventSink
{
    /// <summary>Sends the payload and returns its durable outcome.</summary>
    Task<EventDeliveryOutcome> SendEventAsync(
        FilmberPlaybackPayload payload,
        CancellationToken cancellationToken);
}

/// <summary>
/// Applies at-least-once delivery while keeping a stable client event ID.
/// </summary>
public sealed class EventDeliveryService
{
    private readonly IEventOutbox _outbox;
    private readonly IEventSink _sink;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventDeliveryService"/> class.
    /// </summary>
    public EventDeliveryService(IEventOutbox outbox, IEventSink sink)
    {
        _outbox = outbox;
        _sink = sink;
    }

    /// <summary>
    /// Attempts one ordered delivery pass.
    /// </summary>
    public async Task<int> DeliverOnceAsync(CancellationToken cancellationToken)
    {
        var delivered = 0;
        var events = await _outbox.ReadAsync(cancellationToken).ConfigureAwait(false);
        foreach (var payload in events)
        {
            var outcome = await _sink.SendEventAsync(payload, cancellationToken)
                .ConfigureAwait(false);
            if (outcome == EventDeliveryOutcome.Retry)
            {
                break;
            }

            await _outbox.RemoveAsync(payload.ClientEventId, cancellationToken)
                .ConfigureAwait(false);
            if (outcome == EventDeliveryOutcome.Delivered)
            {
                delivered++;
            }
        }

        return delivered;
    }
}

/// <summary>
/// Periodically flushes the durable Filmber outbox.
/// </summary>
public sealed class EventDeliveryWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private readonly EventDeliveryService _delivery;
    private readonly ILogger<EventDeliveryWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventDeliveryWorker"/> class.
    /// </summary>
    public EventDeliveryWorker(
        EventDeliveryService delivery,
        ILogger<EventDeliveryWorker> logger)
    {
        _delivery = delivery;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delivered = await _delivery.DeliverOnceAsync(stoppingToken)
                    .ConfigureAwait(false);
                if (delivered > 0)
                {
                    _logger.LogInformation(
                        "Filmber Sync delivered {EventCount} queued events.",
                        delivered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Filmber is temporarily unavailable.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Filmber Sync event delivery failed.");
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }
}
