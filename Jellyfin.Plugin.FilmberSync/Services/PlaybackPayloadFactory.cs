using System;
using Jellyfin.Plugin.FilmberSync.Models;

namespace Jellyfin.Plugin.FilmberSync.Services;

/// <summary>
/// Normalizes Jellyfin observations into privacy-safe sync payloads.
/// </summary>
public static class PlaybackPayloadFactory
{
    private const long TicksPerSecond = TimeSpan.TicksPerSecond;

    /// <summary>
    /// Creates a payload with a stable caller-supplied ID.
    /// </summary>
    public static FilmberPlaybackPayload Create(
        PlaybackObservation observation,
        string clientEventId,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientEventId);

        var position = Math.Max(0, observation.PositionTicks);
        var duration = Math.Max(0, observation.DurationTicks);
        var ratio = duration > 0 ? Math.Clamp((double)position / duration, 0, 1) : 0;
        var completed = observation.PlayedToCompletion
            || observation.Event == PlaybackEventKind.Completed;

        var tmdbId = observation.MediaKind == PlaybackMediaKind.Movie
            ? ProviderIdResolver.GetTmdbId(observation.ProviderIds)
            : null;
        var imdbId = observation.MediaKind == PlaybackMediaKind.Movie
            ? ProviderIdResolver.GetImdbId(observation.ProviderIds)
            : null;
        var seriesTmdbId = observation.MediaKind == PlaybackMediaKind.Episode
            ? ProviderIdResolver.GetTmdbId(observation.SeriesProviderIds)
            : null;
        var resolved = observation.MediaKind == PlaybackMediaKind.Movie
            ? tmdbId is not null || imdbId is not null
            : seriesTmdbId is not null
                && observation.SeasonNumber is not null
                && observation.EpisodeNumber is not null;

        return new FilmberPlaybackPayload(
            clientEventId,
            observation.Event,
            observation.MediaKind,
            observation.JellyfinItemId,
            observation.JellyfinSeriesId,
            observation.JellyfinUserId,
            tmdbId,
            imdbId,
            seriesTmdbId,
            observation.SeasonNumber,
            observation.EpisodeNumber,
            observation.Title,
            observation.Year,
            position / TicksPerSecond,
            duration / TicksPerSecond,
            (int)Math.Round(ratio * 100, MidpointRounding.AwayFromZero),
            completed,
            resolved,
            occurredAt);
    }
}
