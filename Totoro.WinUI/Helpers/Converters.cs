using System.ComponentModel;
using System.Text.RegularExpressions;
using AnimDL.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MonoTorrent.Client;
using Totoro.Core.Torrents;

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

    public static Visibility NullToVisibility(object value) => value is null ? Visibility.Collapsed : Visibility.Visible;


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
        if(anime is null)
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
        foreach (var item in ProviderFactory.Instance.Providers)
        {
            scrapersFlyoutItem.Items.Add(new MenuFlyoutItem
            {
                Text = _settings.DefaultProviderType == item.Name ? $"{item.DisplayName} (default)" : item.DisplayName,
                Command = App.Commands.Watch,
                CommandParameter = (anime, item.Name)
            });
        }
        scrapersFlyoutItem.Tapped += (_, _) => App.Commands.Watch.Execute(anime);
        flyout.Items.Add(scrapersFlyoutItem);


        var torrentFlyoutItem = new MenuFlyoutSubItem
        {
            Text = @"Search Torrents",
            Icon = new SymbolIcon { Symbol = Symbol.Globe },
        };
        torrentFlyoutItem.Tapped += (_, _) => App.Commands.SearchTorrent.Execute(anime);

        foreach (var item in Enum.GetValues<TorrentProviderType>().Cast<TorrentProviderType>())
        {
            torrentFlyoutItem.Items.Add(new MenuFlyoutItem
            {
                Text = item == _settings.TorrentProviderType ? $"{item} (default)" : item.ToString(),
                Command = App.Commands.SearchTorrent,
                CommandParameter = (anime, item)
            });
        }
        flyout.Items.Add(torrentFlyoutItem);
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
        if(anime is null)
        {
            return null;
        }

        var flyout = new MenuFlyout();
        foreach (var item in ProviderFactory.Instance.Providers)
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

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex GetNumber();

    [GeneratedRegex(@"(?'Type'.+)\sDub(?'Season'\d+)")]
    private static partial Regex DubRegex();
}