using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.FilmberSync.Models;

namespace Jellyfin.Plugin.FilmberSync.Services;

/// <summary>
/// Persistent queue contract for Filmber events.
/// </summary>
public interface IEventOutbox
{
    /// <summary>Adds an event to the queue.</summary>
    Task EnqueueAsync(FilmberPlaybackPayload payload, CancellationToken cancellationToken);

    /// <summary>Returns an ordered snapshot of queued events.</summary>
    Task<IReadOnlyList<FilmberPlaybackPayload>> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Removes a successfully delivered event.</summary>
    Task RemoveAsync(string clientEventId, CancellationToken cancellationToken);
}

/// <summary>
/// JSON-file outbox stored under the Jellyfin plugin data directory.
/// </summary>
public sealed class FileEventOutbox : IEventOutbox, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc />
    public async Task EnqueueAsync(
        FilmberPlaybackPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            if (items.All(item => item.ClientEventId != payload.ClientEventId))
            {
                items.Add(payload);
                await WriteUnsafeAsync(items, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FilmberPlaybackPayload>> ReadAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(
        string clientEventId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var remaining = items
                .Where(item => item.ClientEventId != clientEventId)
                .ToList();
            if (remaining.Count != items.Count)
            {
                await WriteUnsafeAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _gate.Dispose();
    }

    private static string GetPath()
    {
        var dataFolder = Plugin.Instance?.DataFolderPath
            ?? throw new InvalidOperationException("Filmber Sync plugin is not initialized.");
        Directory.CreateDirectory(dataFolder);
        return Path.Combine(dataFolder, "filmber-event-outbox.json");
    }

    private static async Task<List<FilmberPlaybackPayload>> ReadUnsafeAsync(
        CancellationToken cancellationToken)
    {
        var path = GetPath();
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<FilmberPlaybackPayload>>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static async Task WriteUnsafeAsync(
        IReadOnlyCollection<FilmberPlaybackPayload> items,
        CancellationToken cancellationToken)
    {
        var path = GetPath();
        var temporaryPath = string.Concat(path, ".tmp");
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                items,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, path, true);
    }
}
