
using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Totoro.Core.Torrents;
using Totoro.Core.ViewModels;

namespace Totoro.Core;

public static class Settings
{
    public static Key<bool> PreferSubs { get; } = new("PreferSubs", true);
    public static Key<string> DefaultProviderType { get; } = new("DefaultProviderType", "animepahe");
    public static Key<bool> UseDiscordRichPresense { get; } = new("UseDiscordRichPresense", false);
    public static Key<int> TimeRemainingWhenEpisodeCompletesInSeconds { get; } = new("TimeRemainingWhenEpisodeCompletesInSeconds", 120);
    public static Key<int> OpeningSkipDurationInSeconds { get; } = new("OpeningSkipDurationInSeconds", 85);
    public static Key<Guid> AniSkipId { get; } = new("AniSkipId", Guid.NewGuid);
    public static Key<bool> ContributeTimeStamps { get; } = new("ContributeTimeStamps", false);
    public static Key<LogLevel> MinimumLogLevel { get; } = new("MinimumLogLevel", LogLevel.Debug);
    public static Key<ListServiceType> DefaultListService { get; } = new("DefaultListService", ListServiceType.MyAnimeList);
    public static Key<string> HomePage { get; } = new("HomePage", "Discover");
    public static Key<bool> AllowSideLoadingPlugins { get; } = new("AllowSideLoadingPlugins", false);
    public static Key<StreamQualitySelection> DefaultStreamQualitySelection { get; } = new("DefaultStreamQualitySelection", StreamQualitySelection.Auto);
    public static Key<bool> IncludeNsfw { get; } = new("IncludeNsfw", false);
    public static Key<bool> EnterFullScreenWhenPlaying { get; } = new("EnterFullScreenWhenPlaying", false);
    public static Key<DebridServiceType> DebridServiceType { get; } = new("DebridServiceType", Models.DebridServiceType.Premiumize);
    public static Key<TorrentProviderType> TorrentProviderType { get; } = new("TorrentProviderType", Torrents.TorrentProviderType.AnimeTosho);
    public static Key<string> NyaUrl { get; } = new("NyaUrl", "https://nyaa.ink/");
    public static Key<bool> AutoUpdate { get; } = new("AutoUpdate", true);
    public static Key<string> PremiumizeApiKey { get; } = new("PremiumizeApiKey", "");
    public static Key<AdvanceTorrentSearchOptions> TorrentSearchOptions { get; } = new("TorrentSearchOptions", () => AdvanceTorrentSearchOptions.Default);
    public static Key<MediaPlayerType> MediaPlayerType { get; } = new("MediaPlayerType", Models.MediaPlayerType.WindowsMediaPlayer);

    public static IEnumerable<string> GetObsoleteKeys()
    {
        return typeof(Settings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.GetCustomAttribute<ObsoleteAttribute>() is not null)
            .Select(x => x.GetValue(null) as IKey)
            .Select(x => x.Name);
    }
}
