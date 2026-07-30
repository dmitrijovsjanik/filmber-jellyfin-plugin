using System.Text.Json.Serialization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FilmberSync.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        EnableSync = false;
        FilmberBaseUrl = "https://filmber.online";
        UserMappings = [];
    }

    /// <summary>
    /// Gets or sets a value indicating whether outbound synchronization is enabled.
    /// </summary>
    public bool EnableSync { get; set; }

    /// <summary>
    /// Gets or sets the Filmber origin.
    /// </summary>
    public string FilmberBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the explicit Jellyfin-user mappings.
    /// Tokens are persisted in the plugin XML but excluded from Dashboard JSON.
    /// </summary>
    [JsonIgnore]
    public UserMappingConfiguration[] UserMappings { get; set; }
}

/// <summary>
/// One Jellyfin user explicitly paired with one Filmber external session.
/// </summary>
public class UserMappingConfiguration
{
    /// <summary>Gets or sets the Jellyfin user ID.</summary>
    public string JellyfinUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Jellyfin user name at pairing time.</summary>
    public string JellyfinUserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the Filmber user ID.</summary>
    public string FilmberUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Filmber external session ID.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Gets or sets the bearer token. Never log this value.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISO-8601 token expiry returned by Filmber.</summary>
    public string ExpiresAt { get; set; } = string.Empty;
}
