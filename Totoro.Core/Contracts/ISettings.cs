using Microsoft.Extensions.Logging;

namespace Totoro.Core.Contracts;

public interface ISettings
{
    ElementTheme ElementTheme { get; set; }
    bool PreferSubs { get; set; }
    ProviderType DefaultProviderType { get; set; }
    bool UseDiscordRichPresense { get; set; }
    int TimeRemainingWhenEpisodeCompletesInSeconds { get; set; }
    int OpeningSkipDurationInSeconds { get; set; }
    Guid AniSkipId { get; }
    bool ContributeTimeStamps { get; set; }
    public DefaultUrls DefaultUrls { get; set; }
    LogLevel MinimumLogLevel { get; set; }
    bool AutoUpdate { get; set; }
}

public class DefaultUrls : ReactiveObject
{
    [Reactive] public string GogoAnime { get; set; }
}
