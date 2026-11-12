using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace Totoro.Core.Settings;

/// <summary>
/// Settings' values as text and back: how the text box shows a type without an editor of its own, and how agents read
/// and write settings. <see cref="Format"/> writes what <see cref="TryParse"/> reads.
/// </summary>
public static class SettingText
{
	public static string Format(object? value) => value switch
	{
		null => "",
		Color color => HexColorJsonConverter.ToHex(color),
		string text => text,
		IEnumerable<string> items => string.Join(", ", items),
		DateOnly or DateTime or DateTimeOffset => ((IFormattable)value).ToString("O", CultureInfo.InvariantCulture),
		TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
		TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),
		_ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
	};

	/// <summary>Text as a <paramref name="type"/>: an enum's name ("A, B" for [Flags]), #RRGGBB, items separated by commas...</summary>
	public static bool TryParse(string text, Type type, out object? value)
	{
		value = null;
		if (type == typeof(string))
		{
			value = text;
			return true;
		}

		if (type.IsEnum)
		{
			// Not a number, which would take values the enum doesn't have.
			if (!Enum.TryParse(type, text, ignoreCase: true, out var parsed) || text.Trim() is [>= '0' and <= '9' or '-', ..])
			{
				return false;
			}

			value = parsed;
			return true;
		}

		if (type == typeof(Color))
		{
			value = HexColorJsonConverter.FromHex(text.Trim());
			return value is not null;
		}

		if (typeof(IEnumerable<string>).IsAssignableFrom(type))
		{
			var items = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			value = type.IsArray ? items : items.ToList();
			return type.IsInstanceOfType(value);
		}

		try
		{
			// Numbers, bools, dates, times, durations: invariant, as Format writes them.
			value = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(text);
			return value is not null && type.IsInstanceOfType(value);
		}
		catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException || ex.InnerException is FormatException or OverflowException)
		{
			return false;
		}
	}

	/// <summary>A value as a <paramref name="type"/>: text read with <see cref="TryParse"/>, a number as another kind of number.</summary>
	public static bool TryConvert(object value, Type type, out object? converted)
	{
		converted = null;
		if (value is string text)
		{
			return TryParse(text, type, out converted);
		}

		if (value is IConvertible and not (bool or Enum) && (type.IsPrimitive && type != typeof(bool) || type == typeof(decimal)))
		{
			try
			{
				converted = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
				return true;
			}
			catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
			{
				return false;
			}
		}

		return false;
	}

	/// <summary>What a <paramref name="type"/> takes, written for a person or an agent, e.g. "a date, yyyy-MM-dd".</summary>
	public static string Describe(Type type)
	{
		return type.IsEnum ? $"one of {string.Join(", ", Enum.GetNames(type))}"
			: type == typeof(bool) ? "true or false"
			: type == typeof(Color) ? "a color, #RRGGBB or #AARRGGBB"
			: type == typeof(DateOnly) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ? "a date, yyyy-MM-dd"
			: type == typeof(TimeOnly) ? "a time of day, HH:mm"
			: type == typeof(TimeSpan) ? "a duration, h:mm:ss"
			: type.IsPrimitive || type == typeof(decimal) ? "a number"
			: typeof(IEnumerable<string>).IsAssignableFrom(type) ? "items separated by commas"
			: $"a {type.Name}";
	}
}
