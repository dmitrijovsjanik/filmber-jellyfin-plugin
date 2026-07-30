using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jellyfin.Plugin.FilmberSync.Services;

/// <summary>
/// Resolves exact provider IDs without title-based matching.
/// </summary>
public static class ProviderIdResolver
{
    /// <summary>
    /// Gets a positive TMDB ID from Jellyfin provider IDs.
    /// </summary>
    public static int? GetTmdbId(IReadOnlyDictionary<string, string>? providerIds)
    {
        var raw = GetValue(providerIds, "Tmdb");
        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value > 0
            ? value
            : null;
    }

    /// <summary>
    /// Gets a normalized IMDb title ID.
    /// </summary>
    public static string? GetImdbId(IReadOnlyDictionary<string, string>? providerIds)
    {
        var raw = GetValue(providerIds, "Imdb")?.Trim();
        if (raw is null || raw.Length < 3 || raw.Length > 20)
        {
            return null;
        }

        return raw.StartsWith("tt", StringComparison.OrdinalIgnoreCase)
            ? string.Concat("tt", raw.AsSpan(2))
            : null;
    }

    private static string? GetValue(
        IReadOnlyDictionary<string, string>? providerIds,
        string key)
    {
        if (providerIds is null)
        {
            return null;
        }

        foreach (var pair in providerIds)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }
}
