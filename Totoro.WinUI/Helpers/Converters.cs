using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MonoTorrent.Client;
using Totoro.Core.Services;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Manga;
using Totoro.Plugins.Manga.Contracts;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Options;
using Totoro.Plugins.Torrents.Contracts;
using Totoro.WinUI.Services;

namespace Totoro.WinUI.Helpers;

public static partial class Converters
{
    private static ISettings _settings = App.GetService<ISettings>();

    public static Visibility BooleanToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility InvertedBooleanToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static bool Invert(bool value) => !value;
    public static TEnum ToggleEnum<TEnum>(TEnum current)
        where TEnum : Enum
    {
        var enumType = current.GetType();
        List<string> types = Enum.GetNames(enumType).ToList();
        int index = types.IndexOf(current.ToString());
        if (index == types.Count - 1)
        {
            index = 0;
        }
        else
        {
            index++;
        }
        string nextstring = types[index];
        return (TEnum)Enum.Parse(enumType, nextstring);
    }

    public static string EnumToDescription(object value)
    {
        var fi = value.GetType().GetField(value.ToString());

        if (fi.GetCustomAttributes(typeof(DescriptionAttribute), false) is DescriptionAttribute[] attributes && attributes.Any())
        {
            return attributes.First().Description;
        }

        return value.ToString();
    }

    public static Array EnumToItemSource(Enum value)
    {
        return Enum.GetValues(value.GetType());
    }

    public static string Join(IEnumerable<object> values) => string.Join(",", values);

    public static Visibility NullToVisibility(object value) => value is null ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility InvertedNullToVisibility(object value) => value is null ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility StringToVisibility(string value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;

    public static Brush AiringStatusToBrush(AiringStatus status) => status switch
    {
        AiringStatus.CurrentlyAiring => new SolidColorBrush(Colors.LimeGreen),
        AiringStatus.FinishedAiring => new SolidColorBrush(Colors.MediumSlateBlue),
        AiringStatus.NotYetAired => new SolidColorBrush(Colors.LightSlateGray),
        _ => new SolidColorBrush(Colors.Navy),
    };

    public static Visibility VisibleIfHasTracking(AnimeModel anime) => BooleanToVisibility(anime is { Tracking: not null });
    public static Visibility CollapsedIfHasTracking(AnimeModel anime) => InvertedBooleanToVisibility(anime is { Tracking: not null });

    public static string ConvertStreamType(string type)
    {
        static string getDubedName(string type)
        {
            {
                var match = DubRegex().Match(type);
                return $"{match.Groups["Type"].Value} Season {match.Groups["Season"].Value}";
            }
        }

        return type.ToLower() switch
        {
            "sub" => "Sub",
            "dub" => "Dub",
            string s when s.StartsWith("subbed") => $"Season {GetNumber().Match(s).Groups[1].Value}",
            _ => getDubedName(type)
        };
    }

    public static MenuFlyout AnimeToFlyout(AnimeModel anime)
    {
        if (anime is null)
        {
            return null;
        }

        var flyout = new MenuFlyout();
        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = @"Update",
            Command = App.Commands.UpdateTracking,
            CommandParameter = anime,
            Icon = new SymbolIcon { Symbol = Symbol.PostUpdate },
        });

        var scrapersFlyoutItem = new MenuFlyoutSubItem
        {
            Text = @"Watch",
            Icon = new SymbolIcon { Symbol = Symbol.Video }
        };
        foreach (var item in PluginFactory<AnimeProvider>.Instance.Plugins)
        {
            scrapersFlyoutItem.Items.Add(new MenuFlyoutItem
            {
                Text = _settings.DefaultProviderType == item.Name ? $"{item.DisplayName} (default)" : item.DisplayName,
                Command = App.Commands.Watch,
                CommandParameter = (anime, item.Name)
            });
        }
        flyout.Items.Add(scrapersFlyoutItem);

        var watchExternalFlyoutItem = new MenuFlyoutSubItem
        {
            Text = @"Watch External",
            Icon = new FontIcon { Glyph = "\uEC15" }
        };
        foreach (var item in PluginFactory<AnimeProvider>.Instance.Plugins)
        {
            watchExternalFlyoutItem.Items.Add(new MenuFlyoutItem
            {
                Text = _settings.DefaultProviderType == item.Name ? $"{item.DisplayName} (default)" : item.DisplayName,
                Command = WatchExternal,
                CommandParameter = (anime, item.Name)
            });
        }
        flyout.Items.Add(watchExternalFlyoutItem);

        var torrentFlyoutItem = new MenuFlyoutSubItem
        {
            Text = @"Search Torrents",
            Icon = new SymbolIcon { Symbol = Symbol.Globe },
        };

        foreach (var item in PluginFactory<ITorrentTracker>.Instance.Plugins)
        {
            torrentFlyoutItem.Items.Add(new MenuFlyoutItem
            {
                Text = item.Name == _settings.DefaultTorrentTrackerType ? $"{item.DisplayName} (default)" : item.DisplayName,
                Command = App.Commands.SearchTorrent,
                CommandParameter = (anime, item.Name)
            });
        }
        flyout.Items.Add(torrentFlyoutItem);

        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = @"Set Preferences",
            Command = App.Commands.SetPreferences,
            CommandParameter = anime.Id,
            Icon = new FontIcon() { Glyph = "\uEF58" }
        });
        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = @"Info",
            Command = App.Commands.More,
            CommandParameter = anime.Id,
            Icon = new FontIcon() { Glyph = "\uE946" }
        });

        return flyout;
    }

    public static MenuFlyout GetProvidersFlyout(AnimeModel anime)
    {
        if (anime is null)
        {
            return null;
        }

        var flyout = new MenuFlyout();
        foreach (var item in PluginFactory<AnimeProvider>.Instance.Plugins)
        {
            flyout.Items.Add(new MenuFlyoutItem
            {
                Text = _settings.DefaultProviderType == item.Name ? $"{item.DisplayName} (default)" : item.DisplayName,
                Command = App.Commands.Watch,
                CommandParameter = (anime, item.Name)
            });
        }

        return flyout;
    }

    public static string TorrentToPercent(TorrentManager torrentManager) => $"{torrentManager.Progress:N2}";
    public static string HumanizeTimeSpan(this TimeSpan ts)
    {
        var sb = new StringBuilder();
        var week = ts.Days / 7;
        var days = ts.Days % 7;
        if (week > 0)
        {
            sb.Append($"{week}w ");
        }
        if (days > 0)
        {
            sb.Append($"{days}d ");
        }
        if (ts.Hours > 0)
        {
            sb.Append($"{ts.Hours.ToString().PadLeft(2, '0')}h ");
        }
        if (ts.Minutes > 0)
        {
            sb.Append($"{ts.Minutes.ToString().PadLeft(2, '0')}m ");
        }
        if (ts.Seconds > 0)
        {
            sb.Append($"{ts.Seconds.ToString().PadLeft(2, '0')}s ");
        }

        return sb.ToString().TrimEnd();
    }

    public static ImageSource StreamToImage(Stream stream)
    {
        if (stream is null)
        {
            return null;
        }

        var bmp = new BitmapImage();
        bmp.SetSource(stream.AsRandomAccessStream());
        return bmp;
    }

    public static ImageSource StringToImage(string uri)
    {
        if(string.IsNullOrEmpty(uri))
        {
            return null;
        }

        return new BitmapImage(new Uri(uri));
    }


    public static string ToOneBasedIndex(int number) => (number + 1).ToString();
    public static string ToTitle(ChapterModel chapter) => string.IsNullOrEmpty(chapter.Title)
        ? chapter.Chapter.ToString()
        : $"{chapter.Chapter} - {chapter.Title}";

    public static PluginOptions GetAnimeOptions(string pluginName) => GetOptions<AnimeProvider>(pluginName);
    public static PluginOptions GetTorrentsOptions(string pluginName) => GetOptions<ITorrentTracker>(pluginName);
    public static PluginOptions GetMediaOptions(string pluginName) => GetOptions<INativeMediaPlayer>(pluginName);
    public static PluginOptions GetMangaOptions(string pluginName) => GetOptions<MangaProvider>(pluginName);
    public static string GetAnimePluginVersion(string pluginName) => GetPluginVersion<AnimeProvider>(pluginName);
    public static string GetMangaPluginVersion(string pluginName) => GetPluginVersion<MangaProvider>(pluginName);
    public static string GetTorrentsPluginVersion(string pluginName) => GetPluginVersion<ITorrentTracker>(pluginName);
    public static string GetMediaPluginVersion(string pluginName) => GetPluginVersion<INativeMediaPlayer>(pluginName);
    public static string GetPluginVersion<T>(string pluginName) => PluginFactory<T>.Instance.Plugins.First(x => x.Name == pluginName).Version.ToString();
    
    public static double TiksToSeconds(long value)
    {
        return value / 10000000.0;
    }

    public static long SecondsToTicks(double value)
    {
        return (long)(value * 10000000.0);
    }

    public static string TicksToTime(long value)
    {
        return new TimeSpan(value).ToString("hh\\:mm\\:ss");
    }

    public static string StreamTypeToDisplayName(StreamType streamType)
    {
        if(streamType.AudioLanguage != Languages.Japanese)
        {
            return @$"{streamType.AudioLanguage} Dubbed";
        }
        else if(!string.IsNullOrEmpty(streamType.SubtitleLanguage))
        {
            return @$"{streamType.SubtitleLanguage} Subbed";
        }
        else
        {
            return @"Raw";
        }
    }

    private static PluginOptions GetOptions<T>(string pluginName)
    {
        var options = App.GetService<IPluginOptionsStorage<T>>().GetOptions(pluginName).Options;
        return options;
    }


    [GeneratedRegex(@"(\d+)")]
    private static partial Regex GetNumber();

    [GeneratedRegex(@"(?'Type'.+)\sDub(?'Season'\d+)")]
    private static partial Regex DubRegex();

    public static ICommand WatchExternal { get; set; } = ReactiveCommand.Create<object>(async param =>
    {
        switch (param)
        {
            case (AnimeModel anime, string providerType):
                {
                    await App.GetService<ExternalMediaPlayerLauncher>().Initialize(anime, providerType, App.GetService<ISettings>().DefaultMediaPlayer);
                }
                break;
            case (IAiredAnimeEpisode episode, string providerType):
                {
                    App.GetService<ExternalMediaPlayerLauncher>().Initialize(episode, providerType, App.GetService<ISettings>().DefaultMediaPlayer);
                }
                break;
        }
    });
}