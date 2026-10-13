using System.Text;
using System.Text.RegularExpressions;

namespace Totoro.Modules.AnimeProviders.AnimePahe;

/// <summary>
/// Unpacks JavaScript packed with Dean Edwards' packer (<c>eval(function(p,a,c,k,e,d){...}('...',62,99,'...'.split('|'),0,{}))</c>),
/// as Kwik's player pages are, without running it: each word of the payload is a number, in the given base, into the
/// keyword list.
/// </summary>
internal static partial class JsUnpacker
{
	private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

	/// <summary>The code of each packed script in <paramref name="text"/> (e.g. a page's HTML), unpacked.</summary>
	public static IEnumerable<string> Unpack(string text)
	{
		foreach (Match match in Packed().Matches(text))
		{
			var payload = Unescape(match.Groups["payload"].Value);
			var radix = int.Parse(match.Groups["radix"].Value);
			var keywords = match.Groups["keywords"].Value.Split('|');
			if (radix is < 2 or > 62)
			{
				continue;
			}

			yield return Word().Replace(payload, word =>
			{
				var index = FromBase(word.Value, radix);
				return index >= 0 && index < keywords.Length && keywords[index].Length > 0 ? keywords[index] : word.Value;
			});
		}
	}

	/// <summary>The number <paramref name="word"/> stands for in base <paramref name="radix"/>; -1 if it isn't one.</summary>
	private static int FromBase(string word, int radix)
	{
		var value = 0L;
		foreach (var c in word)
		{
			// Bases up to 36 use lowercase letters only, as JavaScript's toString(radix) does.
			var digit = Digits.IndexOf(radix <= 36 ? char.ToLowerInvariant(c) : c);
			if (digit < 0 || digit >= radix)
			{
				return -1;
			}

			value = value * radix + digit;
			if (value > int.MaxValue)
			{
				return -1;
			}
		}

		return (int)value;
	}

	/// <summary>The payload as a JavaScript string literal in single quotes holds it: \' and \\ unescaped.</summary>
	private static string Unescape(string literal)
	{
		var result = new StringBuilder(literal.Length);
		for (var i = 0; i < literal.Length; i++)
		{
			if (literal[i] == '\\' && i + 1 < literal.Length && literal[i + 1] is '\'' or '\\')
			{
				i++;
			}

			result.Append(literal[i]);
		}

		return result.ToString();
	}

	[GeneratedRegex(@"}\('(?<payload>(?:\\.|[^'\\])*)',\s*(?<radix>\d+),\s*(?<count>\d+),\s*'(?<keywords>(?:\\.|[^'\\])*)'\.split\('\|'\)", RegexOptions.Singleline)]
	private static partial Regex Packed();

	[GeneratedRegex(@"\b\w+\b")]
	private static partial Regex Word();
}
