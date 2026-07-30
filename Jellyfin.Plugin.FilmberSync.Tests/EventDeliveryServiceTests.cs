using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.FilmberSync.Models;
using Jellyfin.Plugin.FilmberSync.Services;
using Xunit;

namespace Jellyfin.Plugin.FilmberSync.Tests;

public sealed class EventDeliveryServiceTests
{
    [Fact]
    public async Task RetriesTheSameClientEventIdAfterTemporaryFailure()
    {
        var payload = new FilmberPlaybackPayload(
            "stable-event-id",
            PlaybackEventKind.Progress,
            PlaybackMediaKind.Movie,
            "item-id",
            null,
            "user-id",
            603,
            null,
            null,
            null,
            null,
            "The Matrix",
            1999,
            60,
            100,
            60,
            false,
            true,
            default);
        var outbox = new MemoryOutbox(payload);
        var sink = new FailOnceSink();
        var delivery = new EventDeliveryService(outbox, sink);

        Assert.Equal(0, await delivery.DeliverOnceAsync(CancellationToken.None));
        Assert.Single(await outbox.ReadAsync(CancellationToken.None));

        Assert.Equal(1, await delivery.DeliverOnceAsync(CancellationToken.None));
        Assert.Empty(await outbox.ReadAsync(CancellationToken.None));
        Assert.Equal(new[] { "stable-event-id", "stable-event-id" }, sink.EventIds);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8787/events", true)]
    [InlineData("http://localhost:8787/events", true)]
    [InlineData("http://host.docker.internal:8787/events", true)]
    [InlineData("https://filmber.online/api/external/sync", true)]
    [InlineData("http://filmber.online", false)]
    [InlineData("not-a-url", false)]
    public void FilmberOriginRequiresHttpsOutsideLocalDevelopment(string url, bool expected)
    {
        Assert.Equal(expected, FilmberApiClient.TryGetBaseUri(url, out _));
    }

    private sealed class MemoryOutbox : IEventOutbox
    {
        private readonly List<FilmberPlaybackPayload> _items;

        public MemoryOutbox(FilmberPlaybackPayload payload)
        {
            _items = [payload];
        }

        public Task EnqueueAsync(
            FilmberPlaybackPayload payload,
            CancellationToken cancellationToken)
        {
            _items.Add(payload);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FilmberPlaybackPayload>> ReadAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<FilmberPlaybackPayload>>([.. _items]);
        }

        public Task RemoveAsync(
            string clientEventId,
            CancellationToken cancellationToken)
        {
            _items.RemoveAll(item => item.ClientEventId == clientEventId);
            return Task.CompletedTask;
        }
    }

    private sealed class FailOnceSink : IEventSink
    {
        private bool _failed;

        public List<string> EventIds { get; } = [];

        public Task<EventDeliveryOutcome> SendEventAsync(
            FilmberPlaybackPayload payload,
            CancellationToken cancellationToken)
        {
            EventIds.Add(payload.ClientEventId);
            if (!_failed)
            {
                _failed = true;
                return Task.FromResult(EventDeliveryOutcome.Retry);
            }

            return Task.FromResult(EventDeliveryOutcome.Delivered);
        }
    }
}
