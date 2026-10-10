using Microsoft.UI.Xaml;

namespace Totoro.Helpers
{
	public static class CommonConverters
	{
		public static Visibility BooleanToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
		public static Visibility InvertedBooleanToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

		public static Visibility VisibleIfAny(string? text) => string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
		public static Visibility VisibleIfNotNull(object? value) => value is null ? Visibility.Collapsed : Visibility.Visible;
		public static Visibility VisibleIfNotEmpty(System.Collections.IEnumerable? items) => items?.GetEnumerator().MoveNext() == true ? Visibility.Visible : Visibility.Collapsed;

		/// <summary>
		/// A URL as an image source. Binding a string straight to Image.Source goes through XamlBindingHelper.ConvertValue,
		/// which throws for a null or empty string; this returns no image instead.
		/// </summary>
		public static Microsoft.UI.Xaml.Media.ImageSource? ToImageSource(string? url) =>
			Uri.TryCreate(url, UriKind.Absolute, out var uri) ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri) : null;
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
