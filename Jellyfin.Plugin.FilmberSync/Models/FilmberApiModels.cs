using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.FilmberSync.Models;

/// <summary>
/// Pair-init response from Filmber.
/// </summary>
public sealed record FilmberPairInitResponse(
    string PairingId,
    string Code,
    string ExpiresAt);

/// <summary>
/// Pair-poll response from Filmber.
/// </summary>
public sealed class FilmberPairPollResponse
{
    /// <summary>Gets or sets the pairing status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the one-shot external token.</summary>
    public string? Token { get; set; }

    /// <summary>Gets or sets the paired Filmber user.</summary>
    public FilmberPairUser? User { get; set; }

    /// <summary>Gets or sets the external session.</summary>
    public FilmberPairSession? Session { get; set; }
}

/// <summary>
/// Filmber user returned by pairing.
/// </summary>
public sealed record FilmberPairUser(string Id, string FirstName);

/// <summary>
/// Filmber external session returned by pairing.
/// </summary>
public sealed record FilmberPairSession(
    string Id,
    string ExpiresAt,
    string ClientType,
    string? ExternalUserId);

/// <summary>
/// One Filmber sync request.
/// </summary>
public sealed record FilmberSyncRequest(
    IReadOnlyList<Dictionary<string, object?>> Ops);

/// <summary>
/// Filmber sync response.
/// </summary>
public sealed class FilmberSyncResponse
{
    /// <summary>Gets or sets operation results.</summary>
    public FilmberSyncResult[] Results { get; set; } = [];
}

/// <summary>
/// One Filmber sync result.
/// </summary>
public sealed class FilmberSyncResult
{
    /// <summary>Gets or sets the stable operation ID.</summary>
    public string? ClientOpId { get; set; }

    /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
    public bool Ok { get; set; }

    /// <summary>Gets or sets the stable error code.</summary>
    public string? Error { get; set; }
}
