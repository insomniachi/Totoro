using System.Text;

namespace Totoro.Core.Services;

/// <summary>Kana as it's written in Latin letters (Hepburn), e.g. "きょう" as "kyou".</summary>
public static class Romaji
{
	private static readonly Dictionary<string, string> s_kana = Table(
		"あ a い i う u え e お o か ka き ki く ku け ke こ ko さ sa し shi す su せ se そ so た ta ち chi つ tsu て te と to " +
		"な na に ni ぬ nu ね ne の no は ha ひ hi ふ fu へ he ほ ho ま ma み mi む mu め me も mo や ya ゆ yu よ yo " +
		"ら ra り ri る ru れ re ろ ro わ wa を o ん n が ga ぎ gi ぐ gu げ ge ご go ざ za じ ji ず zu ぜ ze ぞ zo " +
		"だ da ぢ ji づ zu で de ど do ば ba び bi ぶ bu べ be ぼ bo ぱ pa ぴ pi ぷ pu ぺ pe ぽ po " +
		"ぁ a ぃ i ぅ u ぇ e ぉ o ゃ ya ゅ yu ょ yo ゔ vu " +
		"きゃ kya きゅ kyu きょ kyo しゃ sha しゅ shu しょ sho ちゃ cha ちゅ chu ちょ cho にゃ nya にゅ nyu にょ nyo " +
		"ひゃ hya ひゅ hyu ひょ hyo みゃ mya みゅ myu みょ myo りゃ rya りゅ ryu りょ ryo ぎゃ gya ぎゅ gyu ぎょ gyo " +
		"じゃ ja じゅ ju じょ jo びゃ bya びゅ byu びょ byo ぴゃ pya ぴゅ pyu ぴょ pyo " +
		"ふぁ fa ふぃ fi ふぇ fe ふぉ fo てぃ ti でぃ di うぃ wi うぇ we うぉ wo しぇ she じぇ je ちぇ che " +
		"ゔぁ va ゔぃ vi ゔぇ ve ゔぉ vo");

	/// <summary>The kana (hiragana or katakana) in Latin letters; anything else stays as it is.</summary>
	public static string FromKana(string kana)
	{
		StringBuilder romaji = new();
		var doubled = false;
		for (var i = 0; i < kana.Length; i++)
		{
			var c = ToHiragana(kana[i]);
			if (c == 'っ')
			{
				doubled = true;
				continue;
			}

			if (c == 'ー')
			{
				// A long vowel: the last one again.
				if (romaji.Length > 0 && "aiueo".Contains(romaji[^1]))
				{
					romaji.Append(romaji[^1]);
				}

				continue;
			}

			string? sound = null;
			if (i + 1 < kana.Length && s_kana.TryGetValue($"{c}{ToHiragana(kana[i + 1])}", out sound))
			{
				i++;
			}
			else if (!s_kana.TryGetValue(c.ToString(), out sound))
			{
				sound = kana[i].ToString();
			}

			if (doubled && char.IsAsciiLetter(sound[0]))
			{
				// "っち" is "tchi"; the others double their consonant.
				romaji.Append(sound.StartsWith("ch", StringComparison.Ordinal) ? 't' : sound[0]);
			}

			doubled = false;
			romaji.Append(sound);
		}

		return romaji.ToString();
	}

	/// <summary>The kana with its katakana as hiragana.</summary>
	public static string ToHiragana(string kana) => string.Concat(kana.Select(ToHiragana));

	private static char ToHiragana(char c) => c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c;

	private static Dictionary<string, string> Table(string pairs)
	{
		var parts = pairs.Split(' ');
		Dictionary<string, string> table = [];
		for (var i = 0; i + 1 < parts.Length; i += 2)
		{
			table[parts[i]] = parts[i + 1];
		}

		return table;
	}
}
