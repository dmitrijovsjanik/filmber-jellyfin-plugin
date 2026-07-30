using System;
using System.Collections.Generic;
using Jellyfin.Plugin.FilmberSync.Models;
using Jellyfin.Plugin.FilmberSync.Services;
using Xunit;

namespace Jellyfin.Plugin.FilmberSync.Tests;

public sealed class FilmberEventMapperTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapsMovieProgressToPlayback()
    {
        var operations = FilmberEventMapper.Map(CreateMovie(PlaybackEventKind.Progress));

        var operation = Assert.Single(operations);
        Assert.Equal("playback", operation["op"]);
        Assert.Equal("event-id:playback", operation["clientOpId"]);
        Assert.Equal(603, operation["tmdbId"]);
        Assert.Equal("movie", operation["mediaType"]);
        Assert.Equal(60, operation["percent"]);
    }

    [Fact]
    public void MapsCompletedMovieToPlaybackAndWatched()
    {
        var operations = FilmberEventMapper.Map(CreateMovie(PlaybackEventKind.Completed));

        Assert.Collection(
            operations,
            operation => Assert.Equal("playback", operation["op"]),
            operation =>
            {
                Assert.Equal("watched", operation["op"]);
                Assert.Equal("event-id:watched", operation["clientOpId"]);
            });
    }

    [Fact]
    public void MapsCompletedEpisodeToSeriesPlaybackAndEpisodeWatched()
    {
        var payload = new FilmberPlaybackPayload(
            "episode-event",
            PlaybackEventKind.Completed,
            PlaybackMediaKind.Episode,
            "episode-id",
            "series-id",
            "user-id",
            null,
            null,
            1399,
            2,
            1,
            "The North Remembers",
            2012,
            3000,
            3300,
            91,
            true,
            true,
            OccurredAt);

        var operations = FilmberEventMapper.Map(payload);

        Assert.Equal("tv", operations[0]["mediaType"]);
        Assert.Equal(1399, operations[0]["tmdbId"]);
        Assert.Equal(2, operations[0]["seasonNumber"]);
        Assert.Equal(1, operations[0]["episodeNumber"]);
        Assert.Equal("episode_watched", operations[1]["op"]);
        Assert.Equal(1399, operations[1]["seriesTmdbId"]);
    }

    [Fact]
    public void DoesNotMapUnresolvedPayload()
    {
        var payload = CreateMovie(PlaybackEventKind.Progress) with
        {
            TmdbId = null,
            Resolved = false
        };

        Assert.Empty(FilmberEventMapper.Map(payload));
    }

    private static FilmberPlaybackPayload CreateMovie(PlaybackEventKind eventKind)
    {
        return new FilmberPlaybackPayload(
            "event-id",
            eventKind,
            PlaybackMediaKind.Movie,
            "movie-id",
            null,
            "user-id",
            603,
            "tt0133093",
            null,
            null,
            null,
            "The Matrix",
            1999,
            3600,
            6000,
            60,
            eventKind == PlaybackEventKind.Completed,
            true,
            OccurredAt);
    }
}
