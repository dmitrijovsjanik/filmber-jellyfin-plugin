using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.FilmberSync.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FilmberSync.Services;

/// <summary>
/// Captures server-side Jellyfin playback events for movies and episodes.
/// </summary>
public sealed class PlaybackEventMonitor : IHostedService
{
    private readonly ISessionManager _sessionManager;
    private readonly IEventOutbox _outbox;
    private readonly PluginConfigurationStore _configurationStore;
    private readonly ILogger<PlaybackEventMonitor> _logger;
    private readonly ConcurrentDictionary<string, int> _progressBuckets = new();
    private readonly ConcurrentDictionary<string, byte> _completedPlaybacks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackEventMonitor"/> class.
    /// </summary>
    public PlaybackEventMonitor(
        ISessionManager sessionManager,
        IEventOutbox outbox,
        PluginConfigurationStore configurationStore,
        ILogger<PlaybackEventMonitor> logger)
    {
        _sessionManager = sessionManager;
        _outbox = outbox;
        _configurationStore = configurationStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackProgress += OnPlaybackProgress;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _logger.LogInformation("Filmber Sync playback event monitor started.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        return Task.CompletedTask;
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs eventArgs)
    {
        if (TryGetPlaybackKey(eventArgs, out var key))
        {
            _progressBuckets.TryRemove(key, out _);
            _completedPlaybacks.TryRemove(key, out _);
        }

        Capture(eventArgs, PlaybackEventKind.Start, false);
    }

    private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs eventArgs)
    {
        if (!TryGetBucket(eventArgs, out var key, out var bucket))
        {
            return;
        }

        if (_progressBuckets.TryGetValue(key, out var previous) && previous == bucket)
        {
            return;
        }

        _progressBuckets[key] = bucket;
        Capture(eventArgs, PlaybackEventKind.Progress, false);

        if (bucket >= 85 && _completedPlaybacks.TryAdd(key, 0))
        {
            Capture(eventArgs, PlaybackEventKind.Completed, true);
        }
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs eventArgs)
    {
        var hasKey = TryGetPlaybackKey(eventArgs, out var key);
        Capture(eventArgs, PlaybackEventKind.Stop, eventArgs.PlayedToCompletion);

        if (hasKey
            && IsCompleted(eventArgs)
            && _completedPlaybacks.TryAdd(key, 0))
        {
            Capture(eventArgs, PlaybackEventKind.Completed, true);
        }

        if (hasKey)
        {
            _progressBuckets.TryRemove(key, out _);
            _completedPlaybacks.TryRemove(key, out _);
        }
    }

    private void Capture(
        PlaybackProgressEventArgs eventArgs,
        PlaybackEventKind eventKind,
        bool playedToCompletion)
    {
        if (Plugin.Instance?.Configuration.EnableSync != true)
        {
            return;
        }

        var observation = CreateObservation(
            eventArgs.Item,
            eventArgs.Users,
            eventArgs.PlaybackPositionTicks,
            eventKind,
            playedToCompletion);
        if (observation is null
            || _configurationStore.GetActiveMapping(observation.JellyfinUserId) is null)
        {
            return;
        }

        Queue(observation);
    }

    private void Capture(
        PlaybackStopEventArgs eventArgs,
        PlaybackEventKind eventKind,
        bool playedToCompletion)
    {
        if (Plugin.Instance?.Configuration.EnableSync != true)
        {
            return;
        }

        var observation = CreateObservation(
            eventArgs.Item,
            eventArgs.Users,
            eventArgs.PlaybackPositionTicks,
            eventKind,
            playedToCompletion);
        if (observation is null
            || _configurationStore.GetActiveMapping(observation.JellyfinUserId) is null)
        {
            return;
        }

        Queue(observation);
    }

    private void Queue(PlaybackObservation observation)
    {
        var payload = PlaybackPayloadFactory.Create(
            observation,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow);
        _ = QueueSafelyAsync(payload);
    }

    private async Task QueueSafelyAsync(FilmberPlaybackPayload payload)
    {
        try
        {
            await _outbox.EnqueueAsync(payload, CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation(
                "Filmber Sync queued {EventKind} for {MediaKind}; resolved={Resolved}.",
                payload.Event,
                payload.MediaKind,
                payload.Resolved);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Filmber Sync failed to persist a playback event.");
        }
    }

    private static PlaybackObservation? CreateObservation(
        BaseItem? item,
        IReadOnlyList<User> users,
        long? positionTicks,
        PlaybackEventKind eventKind,
        bool playedToCompletion)
    {
        if (item is null || users.Count == 0)
        {
            return null;
        }

        var common = new
        {
            ItemId = item.Id.ToString("N"),
            UserId = users[0].Id.ToString("N"),
            ProviderIds = CopyProviderIds(item.ProviderIds),
            item.Name,
            item.ProductionYear,
            PositionTicks = positionTicks ?? 0,
            DurationTicks = item.RunTimeTicks ?? 0
        };

        if (item is Movie)
        {
            return new PlaybackObservation(
                eventKind,
                PlaybackMediaKind.Movie,
                common.ItemId,
                common.UserId,
                common.ProviderIds,
                common.Name,
                common.ProductionYear,
                common.PositionTicks,
                common.DurationTicks,
                playedToCompletion);
        }

        if (item is Episode episode && episode.Series is not null)
        {
            return new PlaybackObservation(
                eventKind,
                PlaybackMediaKind.Episode,
                common.ItemId,
                common.UserId,
                common.ProviderIds,
                common.Name,
                common.ProductionYear,
                common.PositionTicks,
                common.DurationTicks,
                playedToCompletion,
                episode.Series.Id.ToString("N"),
                CopyProviderIds(episode.Series.ProviderIds),
                episode.ParentIndexNumber,
                episode.IndexNumber);
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> CopyProviderIds(
        IReadOnlyDictionary<string, string> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetBucket(
        PlaybackProgressEventArgs eventArgs,
        out string key,
        out int bucket)
    {
        key = string.Empty;
        bucket = 0;
        if (eventArgs.Item is null || eventArgs.Users.Count == 0)
        {
            return false;
        }

        var duration = eventArgs.Item.RunTimeTicks ?? 0;
        var position = eventArgs.PlaybackPositionTicks ?? 0;
        if (duration <= 0 || position < 0)
        {
            return false;
        }

        var percent = Math.Clamp((int)Math.Round((double)position / duration * 100), 0, 100);
        bucket = percent >= 85 ? 85 : percent / 5 * 5;
        key = GetPlaybackKey(eventArgs);
        return true;
    }

    private static bool TryGetPlaybackKey(
        PlaybackProgressEventArgs eventArgs,
        out string key)
    {
        key = string.Empty;
        if (eventArgs.Item is null || eventArgs.Users.Count == 0)
        {
            return false;
        }

        key = GetPlaybackKey(eventArgs);
        return true;
    }

    private static string GetPlaybackKey(PlaybackProgressEventArgs eventArgs)
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.PlaySessionId))
        {
            return eventArgs.PlaySessionId;
        }

        return string.Concat(
            eventArgs.DeviceId,
            ":",
            eventArgs.Users[0].Id.ToString("N"),
            ":",
            eventArgs.Item.Id.ToString("N"));
    }

    private static bool IsCompleted(PlaybackStopEventArgs eventArgs)
    {
        if (eventArgs.PlayedToCompletion)
        {
            return true;
        }

        var duration = eventArgs.Item?.RunTimeTicks ?? 0;
        var position = eventArgs.PlaybackPositionTicks ?? 0;
        return duration > 0 && position >= duration * 0.85;
    }
}
