using System.Collections.Generic;
using Jellyfin.Plugin.FilmberSync.Services;
using Xunit;

namespace Jellyfin.Plugin.FilmberSync.Tests;

public sealed class ProviderIdResolverTests
{
    [Fact]
    public void ResolvesTmdbAndImdbCaseInsensitively()
    {
        var providerIds = new Dictionary<string, string>
        {
            ["tmdb"] = "603",
            ["IMDB"] = "tt0133093"
        };

        Assert.Equal(603, ProviderIdResolver.GetTmdbId(providerIds));
        Assert.Equal("tt0133093", ProviderIdResolver.GetImdbId(providerIds));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void RejectsInvalidTmdbIds(string value)
    {
        var providerIds = new Dictionary<string, string> { ["Tmdb"] = value };

        Assert.Null(ProviderIdResolver.GetTmdbId(providerIds));
    }

    [Fact]
    public void ReturnsNullWhenProviderIdsAreMissing()
    {
        Assert.Null(ProviderIdResolver.GetTmdbId(null));
        Assert.Null(ProviderIdResolver.GetImdbId(new Dictionary<string, string>()));
    }
}
