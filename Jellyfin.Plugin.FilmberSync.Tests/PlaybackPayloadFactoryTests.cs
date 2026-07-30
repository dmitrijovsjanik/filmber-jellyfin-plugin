using System;
using System.Collections.Generic;
using System.Text.Json;
using Jellyfin.Plugin.FilmberSync.Models;
using Jellyfin.Plugin.FilmberSync.Services;
using Xunit;

namespace Jellyfin.Plugin.FilmberSync.Tests;

public sealed class PlaybackPayloadFactoryTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildsMoviePayloadWithExactProviderIds()
    {
        var observation = new PlaybackObservation(
            PlaybackEventKind.Progress,
            PlaybackMediaKind.Movie,
            "movie-item-id",
            "user-id",
            new Dictionary<string, string>
            {
                ["Tmdb"] = "603",
                ["Imdb"] = "tt0133093"
            },
            "The Matrix",
            1999,
            TimeSpan.FromMinutes(70).Ticks,
            TimeSpan.FromMinutes(100).Ticks,
            false);

        var payload = PlaybackPayloadFactory.Create(observation, "event-1", OccurredAt);

        Assert.Equal(603, payload.TmdbId);
        Assert.Equal("tt0133093", payload.ImdbId);
        Assert.Equal(70, payload.Percent);
        Assert.False(payload.Completed);
        Assert.True(payload.Resolved);
        Assert.Null(payload.SeriesTmdbId);
    }

    [Fact]
    public void UsesParentSeriesTmdbIdForEpisodePayload()
    {
        var observation = new PlaybackObservation(
            PlaybackEventKind.Stop,
            PlaybackMediaKind.Episode,
            "episode-item-id",
            "user-id",
            new Dictionary<string, string> { ["Tmdb"] = "63056" },
            "The North Remembers",
            2012,
            TimeSpan.FromMinutes(50).Ticks,
            TimeSpan.FromMinutes(55).Ticks,
            false,
            "series-item-id",
            new Dictionary<string, string> { ["Tmdb"] = "1399" },
            2,
            1);

        var payload = PlaybackPayloadFactory.Create(observation, "event-2", OccurredAt);

        Assert.Equal(1399, payload.SeriesTmdbId);
        Assert.Null(payload.TmdbId);
        Assert.Equal(2, payload.SeasonNumber);
        Assert.Equal(1, payload.EpisodeNumber);
        Assert.True(payload.Resolved);
        Assert.False(payload.Completed);
        Assert.Equal(PlaybackEventKind.Stop, payload.Event);
    }

    [Fact]
    public void MarksMissingExactIdentifiersAsUnresolved()
    {
        var movie = new PlaybackObservation(
            PlaybackEventKind.Start,
            PlaybackMediaKind.Movie,
            "movie-item-id",
            "user-id",
            new Dictionary<string, string>(),
            "Similar Title",
            2026,
            0,
            TimeSpan.FromMinutes(90).Ticks,
            false);
        var episode = new PlaybackObservation(
            PlaybackEventKind.Start,
            PlaybackMediaKind.Episode,
            "episode-item-id",
            "user-id",
            new Dictionary<string, string>(),
            "Pilot",
            2026,
            0,
            TimeSpan.FromMinutes(45).Ticks,
            false,
            "series-item-id",
            new Dictionary<string, string>(),
            1,
            1);

        Assert.False(PlaybackPayloadFactory.Create(movie, "event-3", OccurredAt).Resolved);
        Assert.False(PlaybackPayloadFactory.Create(episode, "event-4", OccurredAt).Resolved);
    }

    [Fact]
    public void PayloadCannotContainMediaPathOrPrivateServerAddress()
    {
        var observation = new PlaybackObservation(
            PlaybackEventKind.Start,
            PlaybackMediaKind.Movie,
            "movie-item-id",
            "user-id",
            new Dictionary<string, string> { ["Tmdb"] = "603" },
            "The Matrix",
            1999,
            0,
            TimeSpan.FromMinutes(100).Ticks,
            false);

        var json = JsonSerializer.Serialize(
            PlaybackPayloadFactory.Create(observation, "event-5", OccurredAt));

        Assert.DoesNotContain("Path", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ServerUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Torrent", json, StringComparison.OrdinalIgnoreCase);
    }
}
