using Microsoft.UI.Xaml;

namespace Totoro.WinUI.Helpers;

public static class Converters
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
}