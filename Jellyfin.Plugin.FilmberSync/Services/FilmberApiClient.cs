using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.FilmberSync.Configuration;
using Jellyfin.Plugin.FilmberSync.Models;

namespace Jellyfin.Plugin.FilmberSync.Services;

/// <summary>
/// Calls the confirmed Filmber pairing and sync endpoints.
/// </summary>
public sealed class FilmberApiClient : IEventSink
{
    private readonly HttpClient _httpClient;
    private readonly PluginConfigurationStore _configurationStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilmberApiClient"/> class.
    /// </summary>
    public FilmberApiClient(
        HttpClient httpClient,
        PluginConfigurationStore configurationStore)
    {
        _httpClient = httpClient;
        _configurationStore = configurationStore;
    }

    /// <summary>
    /// Creates a Jellyfin pairing request.
    /// </summary>
    public async Task<FilmberPairInitResponse?> StartPairingAsync(
        string jellyfinUserId,
        string jellyfinUserName,
        CancellationToken cancellationToken)
    {
        if (!TryGetBaseUri(Plugin.Instance?.Configuration.FilmberBaseUrl, out var baseUri))
        {
            return null;
        }

        using var response = await _httpClient.PostAsJsonAsync(
            new Uri(baseUri, "api/external/pair-init"),
            new
            {
                deviceInfo = string.Concat("Jellyfin — ", jellyfinUserName),
                clientType = "jellyfin",
                externalUserId = jellyfinUserId
            },
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<FilmberPairInitResponse>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls one pairing request.
    /// </summary>
    public async Task<FilmberPairPollResponse?> PollPairingAsync(
        string pairingId,
        CancellationToken cancellationToken)
    {
        if (!TryGetBaseUri(Plugin.Instance?.Configuration.FilmberBaseUrl, out var baseUri))
        {
            return null;
        }

        var relative = string.Concat(
            "api/external/pair-poll?id=",
            Uri.EscapeDataString(pairingId));
        return await _httpClient.GetFromJsonAsync<FilmberPairPollResponse>(
            new Uri(baseUri, relative),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends one durable Jellyfin event through Filmber batch sync.
    /// </summary>
    public async Task<EventDeliveryOutcome> SendEventAsync(
        FilmberPlaybackPayload payload,
        CancellationToken cancellationToken)
    {
        var mapping = _configurationStore.GetMapping(payload.JellyfinUserId);
        if (mapping is null || string.IsNullOrWhiteSpace(mapping.AccessToken))
        {
            return EventDeliveryOutcome.Discard;
        }

        if (!TryGetBaseUri(Plugin.Instance?.Configuration.FilmberBaseUrl, out var baseUri))
        {
            return EventDeliveryOutcome.Retry;
        }

        var operations = FilmberEventMapper.Map(payload);
        if (operations.Count == 0)
        {
            return EventDeliveryOutcome.Discard;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(baseUri, "api/external/sync"))
        {
            Content = JsonContent.Create(new FilmberSyncRequest(operations))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            mapping.AccessToken);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return EventDeliveryOutcome.Retry;
        }

        var body = await response.Content.ReadFromJsonAsync<FilmberSyncResponse>(
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (body is null || body.Results.Length != operations.Count)
        {
            return EventDeliveryOutcome.Retry;
        }

        return body.Results.All(result => result.Ok)
            ? EventDeliveryOutcome.Delivered
            : EventDeliveryOutcome.Retry;
    }

    /// <summary>
    /// Validates an origin. Production requires HTTPS; HTTP is local-development only.
    /// </summary>
    public static bool TryGetBaseUri(string? raw, out Uri baseUri)
    {
        baseUri = null!;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        var localDevelopmentHost = parsed.IsLoopback
            || string.Equals(
                parsed.Host,
                "host.docker.internal",
                StringComparison.OrdinalIgnoreCase);
        if (parsed.Scheme != Uri.UriSchemeHttps
            && !(parsed.Scheme == Uri.UriSchemeHttp && localDevelopmentHost))
        {
            return false;
        }

        var builder = new UriBuilder(parsed)
        {
            Path = string.Concat(parsed.AbsolutePath.TrimEnd('/'), "/"),
            Query = string.Empty,
            Fragment = string.Empty
        };
        baseUri = builder.Uri;
        return true;
    }
}
