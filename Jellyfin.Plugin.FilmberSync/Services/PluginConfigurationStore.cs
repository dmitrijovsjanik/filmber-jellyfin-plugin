using System;
using System.Linq;
using Jellyfin.Plugin.FilmberSync.Configuration;

namespace Jellyfin.Plugin.FilmberSync.Services;

/// <summary>
/// Applies synchronized configuration updates without exposing stored tokens.
/// </summary>
public sealed class PluginConfigurationStore
{
    private readonly object _gate = new();

    /// <summary>
    /// Finds an explicit user mapping.
    /// </summary>
    public UserMappingConfiguration? GetMapping(string jellyfinUserId)
    {
        lock (_gate)
        {
            return Plugin.Instance?.Configuration.UserMappings.FirstOrDefault(
                mapping => string.Equals(
                    mapping.JellyfinUserId,
                    jellyfinUserId,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Returns a token-free mapping snapshot.
    /// </summary>
    public UserMappingConfiguration[] GetMappings()
    {
        lock (_gate)
        {
            return Plugin.Instance?.Configuration.UserMappings
                .Select(mapping => new UserMappingConfiguration
                {
                    JellyfinUserId = mapping.JellyfinUserId,
                    JellyfinUserName = mapping.JellyfinUserName,
                    FilmberUserId = mapping.FilmberUserId,
                    SessionId = mapping.SessionId,
                    ExpiresAt = mapping.ExpiresAt
                })
                .ToArray() ?? [];
        }
    }

    /// <summary>
    /// Saves or replaces one explicit mapping.
    /// </summary>
    public void SaveMapping(UserMappingConfiguration mapping)
    {
        lock (_gate)
        {
            var plugin = Plugin.Instance
                ?? throw new InvalidOperationException("Filmber Sync plugin is not initialized.");
            var remaining = plugin.Configuration.UserMappings
                .Where(item => !string.Equals(
                    item.JellyfinUserId,
                    mapping.JellyfinUserId,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            remaining.Add(mapping);
            plugin.Configuration.UserMappings = [.. remaining];
            plugin.SaveConfiguration();
        }
    }

    /// <summary>
    /// Removes one explicit mapping.
    /// </summary>
    public void RemoveMapping(string jellyfinUserId)
    {
        lock (_gate)
        {
            var plugin = Plugin.Instance
                ?? throw new InvalidOperationException("Filmber Sync plugin is not initialized.");
            plugin.Configuration.UserMappings = plugin.Configuration.UserMappings
                .Where(item => !string.Equals(
                    item.JellyfinUserId,
                    jellyfinUserId,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            plugin.SaveConfiguration();
        }
    }

    /// <summary>
    /// Updates non-secret settings.
    /// </summary>
    public void UpdateSettings(bool enableSync, string filmberBaseUrl)
    {
        lock (_gate)
        {
            var plugin = Plugin.Instance
                ?? throw new InvalidOperationException("Filmber Sync plugin is not initialized.");
            plugin.Configuration.EnableSync = enableSync;
            plugin.Configuration.FilmberBaseUrl = filmberBaseUrl;
            plugin.SaveConfiguration();
        }
    }
}
