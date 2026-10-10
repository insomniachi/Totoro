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

	/// <summary>
	/// A nullable number (int?, float?...) as a NumberBox value, where an empty box is NaN. Pass the number's type as
	/// the ConverterParameter ("Int32", "Single"...) so values convert back to it.
	/// </summary>
	public partial class NullableNumberConverter : Microsoft.UI.Xaml.Data.IValueConverter
	{
		public object Convert(object? value, Type targetType, object parameter, string language)
		{
			return value is null ? double.NaN : System.Convert.ToDouble(value);
		}

		public object? ConvertBack(object value, Type targetType, object parameter, string language)
		{
			if (value is not double number || double.IsNaN(number))
			{
				return null;
			}

			var type = Type.GetType($"System.{parameter}") ?? typeof(double);
			if (type == typeof(int) || type == typeof(long))
			{
				number = Math.Round(number);
			}

			return System.Convert.ChangeType(number, type);
		}
	}

	/// <summary>
	/// An optional bound (float?) as one end of a RangeSelector: no bound shows the thumb at the slider's end, given as
	/// the ConverterParameter ("0", "10"...), and moving it back to that end clears the bound.
	/// </summary>
	public partial class OptionalBoundConverter : Microsoft.UI.Xaml.Data.IValueConverter
	{
		public object Convert(object? value, Type targetType, object parameter, string language)
		{
			return value is null ? End(parameter) : System.Convert.ToDouble(value);
		}

		public object? ConvertBack(object value, Type targetType, object parameter, string language)
		{
			return value is double number && number != End(parameter) ? (float)number : null;
		}

		private static double End(object parameter) => double.Parse((string)parameter, System.Globalization.CultureInfo.InvariantCulture);
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
