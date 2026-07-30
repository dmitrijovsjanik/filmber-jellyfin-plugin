using System;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.FilmberSync.Configuration;
using Jellyfin.Plugin.FilmberSync.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.FilmberSync.Controllers;

/// <summary>
/// Administrator-only pairing and configuration API.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("FilmberSync")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class FilmberSyncController : ControllerBase
{
    private readonly IUserManager _userManager;
    private readonly FilmberApiClient _apiClient;
    private readonly PluginConfigurationStore _configurationStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilmberSyncController"/> class.
    /// </summary>
    public FilmberSyncController(
        IUserManager userManager,
        FilmberApiClient apiClient,
        PluginConfigurationStore configurationStore)
    {
        _userManager = userManager;
        _apiClient = apiClient;
        _configurationStore = configurationStore;
    }

    /// <summary>
    /// Returns non-secret settings and local Jellyfin users.
    /// </summary>
    [HttpGet("state")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetState()
    {
        var configuration = Plugin.Instance?.Configuration
            ?? new PluginConfiguration();
        var mappings = _configurationStore.GetMappings();
        var users = _userManager.Users
            .Select(user =>
            {
                var id = user.Id.ToString("N");
                var mapping = mappings.FirstOrDefault(item => string.Equals(
                    item.JellyfinUserId,
                    id,
                    StringComparison.OrdinalIgnoreCase));
                var connected = mapping is not null
                    && PluginConfigurationStore.IsUnexpired(mapping);
                return new
                {
                    id,
                    name = user.Username,
                    paired = connected,
                    status = mapping is null
                        ? "not_connected"
                        : connected
                            ? "connected"
                            : "expired",
                    filmberUserId = mapping?.FilmberUserId,
                    expiresAt = mapping?.ExpiresAt
                };
            })
            .OrderBy(user => user.name)
            .ToArray();
        return Ok(new
        {
            enableSync = configuration.EnableSync,
            filmberBaseUrl = configuration.FilmberBaseUrl,
            users
        });
    }

    /// <summary>
    /// Updates non-secret sync settings.
    /// </summary>
    [HttpPost("settings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        if (!FilmberApiClient.TryGetBaseUri(request.FilmberBaseUrl, out _))
        {
            return BadRequest(new { error = "Filmber URL must use HTTPS or a local development host." });
        }

        _configurationStore.UpdateSettings(
            request.EnableSync,
            request.FilmberBaseUrl.Trim());
        return NoContent();
    }

    /// <summary>
    /// Starts pairing for one explicit Jellyfin user.
    /// </summary>
    [HttpPost("pair/start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> StartPairing(
        [FromBody] PairStartRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(request.JellyfinUserId, "N", out var userId))
        {
            return BadRequest(new { error = "Invalid Jellyfin user ID." });
        }

        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return BadRequest(new { error = "Jellyfin user not found." });
        }

        var result = await _apiClient.StartPairingAsync(
            request.JellyfinUserId,
            user.Username,
            cancellationToken).ConfigureAwait(false);
        return result is null
            ? StatusCode(StatusCodes.Status502BadGateway, new { error = "Filmber pairing is unavailable." })
            : Ok(new
            {
                pairingId = result.PairingId,
                code = result.Code,
                expiresAt = result.ExpiresAt
            });
    }

    /// <summary>
    /// Polls pairing and stores the one-shot token when approved.
    /// </summary>
    [HttpPost("pair/poll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> PollPairing(
        [FromBody] PairPollRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(request.JellyfinUserId, "N", out var userId)
            || _userManager.GetUserById(userId) is null)
        {
            return BadRequest(new { error = "Invalid Jellyfin user ID." });
        }

        var result = await _apiClient.PollPairingAsync(
            request.PairingId,
            cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = "Filmber pairing is unavailable." });
        }

        if (!string.Equals(result.Status, "approved", StringComparison.Ordinal))
        {
            return Ok(new { status = result.Status });
        }

        if (result.Token is null
            || result.User is null
            || result.Session is null
            || !string.Equals(result.Session.ClientType, "jellyfin", StringComparison.Ordinal)
            || !string.Equals(
                result.Session.ExternalUserId,
                request.JellyfinUserId,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Pairing identity mismatch." });
        }

        var userName = _userManager.GetUserById(userId)?.Username ?? string.Empty;
        _configurationStore.SaveMapping(new UserMappingConfiguration
        {
            JellyfinUserId = request.JellyfinUserId,
            JellyfinUserName = userName,
            FilmberUserId = result.User.Id,
            SessionId = result.Session.Id,
            AccessToken = result.Token,
            ExpiresAt = result.Session.ExpiresAt
        });
        return Ok(new { status = "approved" });
    }

    /// <summary>
    /// Revokes the Filmber session and removes the local mapping.
    /// </summary>
    [HttpPost("pair/disconnect")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult> Disconnect(
        [FromBody] DisconnectRequest request,
        CancellationToken cancellationToken)
    {
        var mapping = _configurationStore.GetMapping(request.JellyfinUserId);
        if (mapping is null)
        {
            return NoContent();
        }

        var revoked = await _apiClient.RevokeSessionAsync(
            mapping,
            cancellationToken).ConfigureAwait(false);
        if (!revoked)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = "Filmber session could not be revoked." });
        }

        _configurationStore.RemoveMapping(request.JellyfinUserId);
        return NoContent();
    }
}

/// <summary>Non-secret plugin settings request.</summary>
public sealed record UpdateSettingsRequest(bool EnableSync, string FilmberBaseUrl);

/// <summary>Pair-start request.</summary>
public sealed record PairStartRequest(string JellyfinUserId);

/// <summary>Pair-poll request.</summary>
public sealed record PairPollRequest(string PairingId, string JellyfinUserId);

/// <summary>Disconnect request.</summary>
public sealed record DisconnectRequest(string JellyfinUserId);
