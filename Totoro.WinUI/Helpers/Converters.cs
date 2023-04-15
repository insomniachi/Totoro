using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using MonoTorrent.Client;

namespace Totoro.WinUI.Helpers;

public static partial class Converters
{
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

    public static string TorrentToPercent(TorrentManager torrentManager) => $"{torrentManager.Progress:N2}";

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex GetNumber();

    [GeneratedRegex(@"(?'Type'.+)\sDub(?'Season'\d+)")]
    private static partial Regex DubRegex();
}