using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Totoro.Core.Settings;

/// <summary>A <see cref="Color"/> as "#AARRGGBB": System.Text.Json would write its properties and can't read them back.</summary>
public sealed class HexColorJsonConverter : JsonConverter<Color>
{
	public static string ToHex(Color color) => $"#{color.ToArgb():X8}";

	/// <summary>"#AARRGGBB" or "#RRGGBB"; null if it's neither.</summary>
	public static Color? FromHex(string? text)
	{
		if (text is ['#', .. var hex] && hex.Length is 6 or 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
		{
			return Color.FromArgb(unchecked((int)(hex.Length == 6 ? argb | 0xFF000000 : argb)));
		}

		return null;
	}

	public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return FromHex(reader.GetString()) ?? throw new JsonException($"\"{reader.GetString()}\" isn't a color: it takes #AARRGGBB.");
	}

	public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(ToHex(value));
	}
}
