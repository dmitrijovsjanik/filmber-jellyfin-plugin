using System;
using System.Collections.Generic;
using Jellyfin.Plugin.FilmberSync.Models;

namespace Jellyfin.Plugin.FilmberSync.Services;

/// <summary>
/// Maps privacy-safe Jellyfin observations to the confirmed Filmber sync contract.
/// </summary>
public static class FilmberEventMapper
{
    /// <summary>
    /// Creates zero or more idempotent Filmber operations.
    /// </summary>
    public static IReadOnlyList<Dictionary<string, object?>> Map(
        FilmberPlaybackPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!payload.Resolved)
        {
            return [];
        }

        var tmdbId = payload.MediaKind == PlaybackMediaKind.Movie
            ? payload.TmdbId
            : payload.SeriesTmdbId;
        if (tmdbId is null)
        {
            return [];
        }

        var timestamp = payload.OccurredAt.ToUnixTimeMilliseconds();
        var playback = new Dictionary<string, object?>
        {
            ["op"] = "playback",
            ["clientOpId"] = string.Concat(payload.ClientEventId, ":playback"),
            ["ts"] = timestamp,
            ["tmdbId"] = tmdbId.Value,
            ["mediaType"] = payload.MediaKind == PlaybackMediaKind.Movie ? "movie" : "tv",
            ["percent"] = payload.Percent,
            ["positionSeconds"] = payload.PositionSeconds,
            ["durationSeconds"] = payload.DurationSeconds,
            ["completed"] = payload.Completed,
            ["lastPlayedAt"] = payload.OccurredAt.ToString("O")
        };
        if (payload.MediaKind == PlaybackMediaKind.Episode)
        {
            playback["seasonNumber"] = payload.SeasonNumber;
            playback["episodeNumber"] = payload.EpisodeNumber;
        }

        var operations = new List<Dictionary<string, object?>> { playback };
        if (payload.Event != PlaybackEventKind.Completed)
        {
            return operations;
        }

        if (payload.MediaKind == PlaybackMediaKind.Movie)
        {
            operations.Add(new Dictionary<string, object?>
            {
                ["op"] = "watched",
                ["clientOpId"] = string.Concat(payload.ClientEventId, ":watched"),
                ["ts"] = timestamp,
                ["tmdbId"] = tmdbId.Value,
                ["mediaType"] = "movie",
                ["watchedAt"] = payload.OccurredAt.ToString("O")
            });
        }
        else
        {
            operations.Add(new Dictionary<string, object?>
            {
                ["op"] = "episode_watched",
                ["clientOpId"] = string.Concat(payload.ClientEventId, ":episode"),
                ["ts"] = timestamp,
                ["seriesTmdbId"] = tmdbId.Value,
                ["seasonNumber"] = payload.SeasonNumber,
                ["episodeNumber"] = payload.EpisodeNumber,
                ["watchedAt"] = payload.OccurredAt.ToString("O")
            });
        }

        return operations;
    }
}
