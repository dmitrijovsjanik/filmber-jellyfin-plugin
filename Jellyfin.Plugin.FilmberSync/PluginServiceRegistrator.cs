using Jellyfin.Plugin.FilmberSync.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.FilmberSync;

/// <summary>
/// Registers plugin services in Jellyfin.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<PluginConfigurationStore>();
        serviceCollection.AddSingleton<IEventOutbox, FileEventOutbox>();
        serviceCollection.AddHttpClient<FilmberApiClient>();
        serviceCollection.AddSingleton<IEventSink>(
            provider => provider.GetRequiredService<FilmberApiClient>());
        serviceCollection.AddSingleton<EventDeliveryService>();
        serviceCollection.AddHostedService<EventDeliveryWorker>();
        serviceCollection.AddHostedService<PlaybackEventMonitor>();
    }
}
