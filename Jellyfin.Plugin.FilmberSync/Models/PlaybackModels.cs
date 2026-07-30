using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.FilmberSync.Models;

/// <summary>
/// Playback lifecycle event kinds captured from Jellyfin.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaybackEventKind
{
    /// <summary>Playback started.</summary>
    Start,

    /// <summary>Playback position changed.</summary>
    Progress,

    /// <summary>Playback stopped before confirmed completion.</summary>
    Stop,

    /// <summary>Playback reached the confirmed completion threshold.</summary>
    Completed
}

/// <summary>
/// Media kind supported by Filmber synchronization.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaybackMediaKind
{
    /// <summary>A movie.</summary>
    Movie,

    /// <summary>A series episode.</summary>
    Episode
}

/// <summary>
/// Jellyfin data extracted before payload normalization.
/// </summary>
public sealed record PlaybackObservation(
    PlaybackEventKind Event,
    PlaybackMediaKind MediaKind,
    string JellyfinItemId,
    string JellyfinUserId,
    IReadOnlyDictionary<string, string> ProviderIds,
    string? Title,
    int? Year,
    long PositionTicks,
    long DurationTicks,
    bool PlayedToCompletion,
    string? JellyfinSeriesId = null,
    IReadOnlyDictionary<string, string>? SeriesProviderIds = null,
    int? SeasonNumber = null,
    int? EpisodeNumber = null);

/// <summary>
/// Privacy-safe payload emitted by the playback monitor.
/// </summary>
public sealed record FilmberPlaybackPayload(
    string ClientEventId,
    PlaybackEventKind Event,
    PlaybackMediaKind MediaKind,
    string JellyfinItemId,
    string? JellyfinSeriesId,
    string JellyfinUserId,
    int? TmdbId,
    string? ImdbId,
    int? SeriesTmdbId,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? Title,
    int? Year,
    long PositionSeconds,
    long DurationSeconds,
    int Percent,
    bool Completed,
    bool Resolved,
    DateTimeOffset OccurredAt);
