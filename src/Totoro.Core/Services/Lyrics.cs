using System.Text.RegularExpressions;

namespace Totoro.Core.Services;

/// <summary>A word of a lyric line, and when it starts being sung.</summary>
public sealed record LyricWord(TimeSpan Start, string Text);

/// <param name="Words">The line's words with their times; empty if only the line is timed.</param>
public sealed record LyricLine(TimeSpan Start, TimeSpan End, string Text, IReadOnlyList<LyricWord> Words);

/// <summary>What a song's lyrics show: the Japanese with or without its readings, or the song's English lyrics.</summary>
public enum LyricReadings
{
	None,
	Furigana,
	Romaji,
	Both,

	/// <summary>The English lyrics, where there are any; otherwise as <see cref="Both"/>.</summary>
	English,
}

/// <param name="Lines">The lyrics as they're sung; empty if they aren't known.</param>
/// <param name="English">The song's lyrics in English; empty if there are none.</param>
public sealed record SongLyrics(IReadOnlyList<LyricLine> Lines, IReadOnlyList<LyricLine> English)
{
	public static SongLyrics None { get; } = new([], []);
}

/// <summary>Reads LRCLIB's lyrics records: their Lyricsfile (YAML, with line and word times), and which of them suits a song.</summary>
public static partial class Lyrics
{
	public static bool IsKanji(char c) => c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿') or '々';

	/// <summary>Kana or kanji.</summary>
	public static bool IsJapanese(char c) => c is >= '぀' and <= 'ヿ' || IsKanji(c);

	/// <summary>
	/// How well a record suits a song whose audio is <paramref name="audio"/> long: one in Japanese script (what's sung,
	/// rather than a translated version of the song; and what readings can be made for) before one of its length (its
	/// times fit), before one with word times.
	/// </summary>
	// By its timed lines, not the record's other fields: a record can time a translated version and carry the Japanese untimed.
	internal static int Score(double recordSeconds, IReadOnlyList<LyricLine> lines, TimeSpan audio)
	{
		return (lines.Any(x => x.Text.Any(IsJapanese)) ? 4 : 0)
			+ (Math.Abs(recordSeconds - audio.TotalSeconds) <= 3 ? 2 : 0)
			+ (lines.Any(x => x.Words.Count > 0) ? 1 : 0);
	}

	/// <summary>Whether the lines are in English, rather than Japanese in its script or in Latin letters.</summary>
	// shortcut: goes by common English words in a third of the lines; use the record's language when LRCLIB fills it reliably.
	internal static bool IsEnglish(IReadOnlyList<LyricLine> lines)
	{
		return lines.Count > 0
			&& !lines.Any(x => x.Text.Any(IsJapanese))
			&& lines.Count(x => EnglishWord().IsMatch(x.Text)) * 3 >= lines.Count;
	}

	[GeneratedRegex(@"\b(the|you|your|and|is|it|of|with|that|this|what|for)\b", RegexOptions.IgnoreCase)]
	private static partial Regex EnglishWord();

	/// <summary>The lines of a Lyricsfile, in order; none if it has none.</summary>
	// shortcut: reads only lrclib's flat Lyricsfile layout (one key per line); use YamlDotNet if the format grows.
	internal static IReadOnlyList<LyricLine> Parse(string yaml)
	{
		List<LyricLine> lines = [];
		List<LyricWord> words = [];
		var inLines = false;
		var lineIndent = -1;
		foreach (var raw in yaml.Split('\n'))
		{
			var row = raw.TrimEnd('\r');
			if (!inLines)
			{
				inLines = row == "lines:";
				continue;
			}

			var content = row.TrimStart();
			if (content.Length == 0)
			{
				continue;
			}

			var indent = row.Length - content.Length;
			var isItem = content.StartsWith("- ", StringComparison.Ordinal);
			if (indent == 0 && !isItem)
			{
				// The next top-level key: the lines are over.
				break;
			}

			if (isItem)
			{
				content = content[2..];
				lineIndent = lineIndent < 0 ? indent : lineIndent;
			}

			var colon = content.IndexOf(':');
			if (colon < 0 || lineIndent < 0)
			{
				continue;
			}

			var key = content[..colon];
			var value = Unquote(content[(colon + 1)..].Trim());
			// A line's keys are two in from its "- "; its words' are further in.
			var isWord = isItem ? indent > lineIndent : indent > lineIndent + 2;
			if (isWord)
			{
				if (isItem)
				{
					words.Add(new LyricWord(TimeSpan.Zero, ""));
				}

				if (words.Count > 0)
				{
					words[^1] = key switch
					{
						"text" => words[^1] with { Text = value },
						"start_ms" => words[^1] with { Start = Milliseconds(value) },
						_ => words[^1],
					};
				}

				continue;
			}

			if (isItem)
			{
				words = [];
				lines.Add(new LyricLine(TimeSpan.Zero, TimeSpan.Zero, "", words));
			}

			lines[^1] = key switch
			{
				"text" => lines[^1] with { Text = value },
				"start_ms" => lines[^1] with { Start = Milliseconds(value) },
				"end_ms" => lines[^1] with { End = Milliseconds(value) },
				_ => lines[^1],
			};
		}

		// A line without an end lasts until the next one.
		for (var i = 0; i < lines.Count; i++)
		{
			if (lines[i].End <= lines[i].Start)
			{
				lines[i] = lines[i] with { End = i + 1 < lines.Count ? lines[i + 1].Start : lines[i].Start + TimeSpan.FromSeconds(5) };
			}
		}

		return lines;
	}

	private static TimeSpan Milliseconds(string value) => TimeSpan.FromMilliseconds(double.TryParse(value, out var ms) ? ms : 0);

	private static string Unquote(string value)
	{
		if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
		{
			return value[1..^1].Replace("''", "'");
		}

		return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? Regex.Unescape(value[1..^1]) : value;
	}
}
