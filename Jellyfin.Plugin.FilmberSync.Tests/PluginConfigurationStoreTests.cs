using System;
using Jellyfin.Plugin.FilmberSync.Configuration;
using Jellyfin.Plugin.FilmberSync.Services;
using Xunit;

namespace Jellyfin.Plugin.FilmberSync.Tests;

public sealed class PluginConfigurationStoreTests
{
    [Fact]
    public void IsActiveAcceptsUnexpiredMappingWithToken()
    {
        var mapping = new UserMappingConfiguration
        {
            AccessToken = "token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToString("O")
        };

        Assert.True(PluginConfigurationStore.IsActive(mapping));
    }

    [Theory]
    [InlineData("", "2099-01-01T00:00:00Z")]
    [InlineData("token", "2020-01-01T00:00:00Z")]
    [InlineData("token", "not-a-date")]
    public void IsActiveRejectsUnusableMapping(string token, string expiresAt)
    {
        var mapping = new UserMappingConfiguration
        {
            AccessToken = token,
            ExpiresAt = expiresAt
        };

        Assert.False(PluginConfigurationStore.IsActive(mapping));
    }

    [Fact]
    public void IsUnexpiredDoesNotRequireTokenInDashboardSnapshot()
    {
        var mapping = new UserMappingConfiguration
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToString("O")
        };

        Assert.True(PluginConfigurationStore.IsUnexpired(mapping));
    }
}
