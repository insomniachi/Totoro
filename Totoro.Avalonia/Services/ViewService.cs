using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using Totoro.Core.Contracts;
using Totoro.Core.Models;
using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Avalonia.Services;

public class ViewService : IViewService
{
    public Task<Unit> UpdateTracking(IAnimeModel anime)
    {
        return Task.FromResult(Unit.Default);
    }

    public Task<int> RequestRating(IAnimeModel anime)
    {
        return Task.FromResult(0);
    }

    public Task<ICatalogItem> ChooseSearchResult(ICatalogItem closesMatch, List<ICatalogItem> searchResults, string providerType)
    {
        return Task.FromResult<ICatalogItem>(default!);
    }

    public Task Authenticate(ListServiceType type)
    {
        return Task.CompletedTask;
    }

    public Task PlayVideo(string title, string url)
    {
        return Task.CompletedTask;
    }

    public Task<T> SelectModel<T>(IEnumerable<T> models, T defaultValue = default, Func<string, IAsyncEnumerable<T>> searcher = default) where T : class
    {
        return Task.FromResult<T>(default!);
    }

    public Task SubmitTimeStamp(long malId, int ep, VideoStreamModel stream, TimestampResult existingResult, double duration,
        double introStart)
    {
        return Task.CompletedTask;
    }

    public Task<bool> Question(string title, string message)
    {
        return Task.FromResult(false);
    }

    public Task<Unit> Information(string title, string message)
    {
        return Task.FromResult(Unit.Default);
    }

    public Task<string> BrowseFolder()
    {
        return Task.FromResult("");
    }

    public Task<string> BrowseSubtitle()
    {
        return Task.FromResult("");
    }

    public Task UnhandledException(Exception ex)
    {
        return Task.CompletedTask;
    }

    public Task ShowPluginStore(string pluginType)
    {
        return Task.CompletedTask;
    }

    public Task PromptPreferences(long id)
    {
        return Task.CompletedTask;
    }

    public Task ShowSearchListServiceDialog()
    {
        return Task.CompletedTask;
    }
}