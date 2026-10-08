using Microsoft.UI.Xaml;

namespace Totoro.Helpers
{
	public static class CommonConverters
	{
		public static Visibility BooleanToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
		public static Visibility InvertedBooleanToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
	}

	public partial class InvertedBooleanToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value is bool boolValue)
			{
				return boolValue ? Visibility.Collapsed : Visibility.Visible;
			}
			return Visibility.Visible;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			if (value is Visibility visibility)
			{
				return visibility != Visibility.Visible;
			}
			return false;
		}
	}

	public partial class BooleanToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value is bool boolValue)
			{
				return boolValue ? Visibility.Visible : Visibility.Collapsed;
			}
			return Visibility.Collapsed;
		}
		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			if (value is Visibility visibility)
			{
				return visibility == Visibility.Visible;
			}
			return false;
		}
	}
}
