using Totoro.Core.Services;
using Totoro.UI.Controls;
using Xunit;

namespace Totoro.Tests;

public class LyricsTests
{
	// Made-up lines, in LRCLIB's Lyricsfile layout.
	private const string Lyricsfile = """
		version: '1.0'
		metadata:
		  title: Test
		  duration_ms: 90000
		lines:
		- text: one two
		  start_ms: 500
		  end_ms: 2000
		  words:
		  - text: 'one '
		    start_ms: 500
		  - text: two
		    start_ms: 1200
		- text: 'it''s "quoted"'
		  start_ms: 2500
		- text: "tab\there あ"
		  start_ms: 4000
		  end_ms: 5000
		""";

	private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

	[Fact]
	public void Parse_LinesAndWords()
	{
		var lines = Lyrics.Parse(Lyricsfile);

		Assert.Equal(3, lines.Count);
		Assert.Equal(("one two", Ms(500), Ms(2000)), (lines[0].Text, lines[0].Start, lines[0].End));
		Assert.Equal([new LyricWord(Ms(500), "one "), new LyricWord(Ms(1200), "two")], lines[0].Words);
	}

	[Fact]
	public void Parse_QuotedText_AndEndFromNextLine()
	{
		var lines = Lyrics.Parse(Lyricsfile);

		Assert.Equal("it's \"quoted\"", lines[1].Text);
		Assert.Empty(lines[1].Words);
		// No end of its own: until the next line.
		Assert.Equal(Ms(4000), lines[1].End);
		Assert.Equal("tab\there あ", lines[2].Text);
	}

	[Fact]
	public void Parse_NoLines() => Assert.Empty(Lyrics.Parse("version: '1.0'\nmetadata:\n  instrumental: true\n"));

	[Theory]
	[InlineData("きょう", "kyou")]
	[InlineData("がっこう", "gakkou")]
	[InlineData("まっちゃ", "matcha")]
	[InlineData("しんぶん", "shinbun")]
	[InlineData("スーパー", "suupaa")]
	[InlineData("ファイト!", "faito!")]
	public void Romaji_FromKana(string kana, string expected) => Assert.Equal(expected, Romaji.FromKana(kana));

	[Fact]
	public void Score_JapaneseThenLengthThenWords()
	{
		var audio = TimeSpan.FromSeconds(90);
		static IReadOnlyList<LyricLine> Lines(string text, bool words) =>
			[new(TimeSpan.Zero, TimeSpan.FromSeconds(1), text, words ? [new LyricWord(TimeSpan.Zero, text)] : [])];
		var tvSizeLatinWords = Lyrics.Score(91, Lines("abc", words: true), audio);
		var tvSizeJapanese = Lyrics.Score(91, Lines("あ", words: false), audio);
		var fullJapaneseWords = Lyrics.Score(240, Lines("あ", words: true), audio);
		var fullJapanese = Lyrics.Score(240, Lines("あ", words: false), audio);

		Assert.True(tvSizeJapanese > fullJapaneseWords);
		Assert.True(fullJapaneseWords > fullJapanese);
		Assert.True(fullJapanese > tvSizeLatinWords);
	}

	[Fact]
	public void IsEnglish_NotJapaneseInEitherScript()
	{
		static IReadOnlyList<LyricLine> Lines(params string[] texts) =>
			[.. texts.Select(x => new LyricLine(TimeSpan.Zero, TimeSpan.FromSeconds(1), x, []))];

		Assert.True(Lyrics.IsEnglish(Lines("this is a test line", "walking home", "what a day for you")));
		// Japanese in Latin letters.
		Assert.False(Lyrics.IsEnglish(Lines("kyou wa ii tenki desu", "ashita mo hareru", "sora ga aoi")));
		Assert.False(Lyrics.IsEnglish(Lines("今日はいい天気です", "this is the day")));
		Assert.False(Lyrics.IsEnglish([]));
	}

	[Fact]
	public void Split_JapaneseWordsWithReadings_OthersWithout()
	{
		var cells = LyricsView.Split("今日はいい天気です OK go").ToList();

		Assert.Contains(("今日", "きょう"), cells);
		Assert.Contains(("天気", "てんき"), cells);
		Assert.Equal([("OK", null), ("go", null)], cells.TakeLast(2));
		// Nothing lost or added.
		Assert.Equal("今日はいい天気ですOKgo", string.Concat(cells.Select(x => x.Text)));
	}

	[Fact]
	public void Places_TheLineSungInTheMiddle()
	{
		var lines = Lyrics.Parse(Lyricsfile);

		// Before the first line: it waits below the middle.
		Assert.Equal([(lines[0], 2)], LyricsView.Places(lines, -1));
		Assert.Equal([(lines[0], 1), (lines[1], 2)], LyricsView.Places(lines, 0));
		Assert.Equal([(lines[0], 0), (lines[1], 1), (lines[2], 2)], LyricsView.Places(lines, 1));
		// The last line: nothing after it.
		Assert.Equal([(lines[1], 0), (lines[2], 1)], LyricsView.Places(lines, 2));
	}

	[Fact]
	public void StartOf_TheWordTheOffsetIsIn()
	{
		var line = Lyrics.Parse(Lyricsfile)[0];

		Assert.Equal(Ms(500), LyricsView.StartOf(line, 0));
		Assert.Equal(Ms(500), LyricsView.StartOf(line, 2));
		Assert.Equal(Ms(1200), LyricsView.StartOf(line, 3));
		// Only the line is timed: its start.
		Assert.Equal(Ms(2500), LyricsView.StartOf(Lyrics.Parse(Lyricsfile)[1], 4));
	}
}
