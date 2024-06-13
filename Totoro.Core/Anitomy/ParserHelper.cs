

using System.Text;

namespace Anitomy;


/// <summary>
/// Utility class to assist in the parsing.
/// </summary>
public class ParserHelper(Parser parser)
{
    private const string Dashes = "-\u2010\u2011\u2012\u2013\u2014\u2015";
    private const string DashesWithSpace = " -\u2010\u2011\u2012\u2013\u2014\u2015";

    private static readonly Dictionary<string, string> Ordinals = new Dictionary<string, string>
    {
      {"1st", "1"}, {"First", "1"},
      {"2nd", "2"}, {"Second", "2"},
      {"3rd", "3"}, {"Third", "3"},
      {"4th", "4"}, {"Fourth", "4"},
      {"5th", "5"}, {"Fifth", "5"},
      {"6th", "6"}, {"Sixth", "6"},
      {"7th", "7"}, {"Seventh", "7"},
      {"8th", "8"}, {"Eighth", "8"},
      {"9th", "9"}, {"Ninth", "9"}
    };

    private readonly Parser _parser = parser;

    /// <summary>
    /// Returns whether or not the <code>result</code> matches the <code>category</code>.
    /// </summary>
    public bool IsTokenCategory(int result, Token.TokenCategory category)
    {
        return Token.InListRange(result, _parser.Tokens) && _parser.Tokens[result].Category == category;
    }

    /// <summary>
    /// Returns whether or not the <code>str</code> is a CRC string.
    /// </summary>
    public static bool IsCrc32(string str)
    {
        return str != null && str.Length == 8 && StringHelper.IsHexadecimalString(str);
    }

    /// <summary>
    /// Returns whether or not the <code>character</code> is a dash character
    /// </summary>
    public static bool IsDashCharacter(char c)
    {
        return Dashes.Contains(c.ToString());
    }

    /// <summary>
    /// Returns a number from an original (e.g. 2nd)
    /// </summary>
    private static string GetNumberFromOrdinal(string str)
    {
        return string.IsNullOrEmpty(str) ? "" : Ordinals.TryGetValue(str, out var foundString) ? foundString : "";
    }

    /// <summary>
    /// Returns the index of the first digit in the <code>str</code>; -1 otherwise.
    /// </summary>
    public static int IndexOfFirstDigit(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return -1;
        }

        for (var i = 0; i < str.Length; i++)
        {
            if (char.IsDigit(str, i))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Returns whether or not the <code>str</code> is a resolution.
    /// </summary>
    public static bool IsResolution(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return false;
        }

        const int minWidthSize = 3;
        const int minHeightSize = 3;

        if (str.Length >= minWidthSize + 1 + minHeightSize)
        {
            var pos = str.IndexOfAny("xX\u00D7".ToCharArray());
            return pos != -1 && pos >= minWidthSize && pos <= str.Length - (minHeightSize + 1) && !str.Where((t, i) => i != pos && !char.IsDigit(t)).Any();
        }

        if (str.Length < minHeightSize + 1)
        {
            return false;
        }

        if (char.ToLower(str[str.Length - 1]) != 'p')
        {
            return false;
        }

        for (var i = 0; i < str.Length - 1; i++)
        {
            if (!char.IsDigit(str[i]))
            {
                return false;
            }
        }

        return true;

    }

    /// <summary>
    /// Returns whether or not the <code>category</code> is searchable.
    /// </summary>
    public static bool IsElementCategorySearchable(ElementCategory category) => category switch
    {
        ElementCategory.AnimeSeasonPrefix or
        ElementCategory.AnimeType or
        ElementCategory.AudioTerm or
        ElementCategory.DeviceCompatibility or
        ElementCategory.EpisodePrefix or
        ElementCategory.FileChecksum or
        ElementCategory.Language or
        ElementCategory.Other or
        ElementCategory.ReleaseGroup or
        ElementCategory.ReleaseInformation or
        ElementCategory.ReleaseVersion or
        ElementCategory.Source or
        ElementCategory.Subtitles or
        ElementCategory.VideoResolution or
        ElementCategory.VideoTerm or
        ElementCategory.VolumePrefix => true,
        _ => false,
    };

    /// <summary>
    /// Returns whether the <code>category</code> is singular.
    /// </summary>
    public static bool IsElementCategorySingular(ElementCategory category) => category switch
    {
        ElementCategory.AnimeSeason or
        ElementCategory.AnimeType or
        ElementCategory.AudioTerm or
        ElementCategory.DeviceCompatibility or
        ElementCategory.EpisodeNumber or
        ElementCategory.Language or
        ElementCategory.Other or
        ElementCategory.ReleaseInformation or
        ElementCategory.Source or
        ElementCategory.VideoTerm => false,
        _ => false,
    };

    /// <summary>
    /// Returns whether or not a token at the current <code>pos</code> is isolated(surrounded by braces).
    /// </summary>
    public bool IsTokenIsolated(int pos)
    {
        var prevToken = Token.FindPrevToken(_parser.Tokens, pos, Token.TokenFlag.FlagNotDelimiter);
        if (!IsTokenCategory(prevToken, Token.TokenCategory.Bracket))
        {
            return false;
        }

        var nextToken = Token.FindNextToken(_parser.Tokens, pos, Token.TokenFlag.FlagNotDelimiter);
        return IsTokenCategory(nextToken, Token.TokenCategory.Bracket);
    }

    /// <summary>
    /// Finds and sets the anime season keyword.
    /// </summary>
    public bool CheckAndSetAnimeSeasonKeyword(Token token, int currentTokenPos)
    {
        void SetAnimeSeason(Token first, Token second, string content)
        {
            _parser.Elements.Add(new Element(ElementCategory.AnimeSeason, content));
            first.Category = Token.TokenCategory.Identifier;
            second.Category = Token.TokenCategory.Identifier;
        }

        var previousToken = Token.FindPrevToken(_parser.Tokens, currentTokenPos, Token.TokenFlag.FlagNotDelimiter);
        if (Token.InListRange(previousToken, _parser.Tokens))
        {
            var number = GetNumberFromOrdinal(_parser.Tokens[previousToken].Content);
            if (!string.IsNullOrEmpty(number))
            {
                SetAnimeSeason(_parser.Tokens[previousToken], token, number);
                return true;
            }
        }

        var nextToken = Token.FindNextToken(_parser.Tokens, currentTokenPos, Token.TokenFlag.FlagNotDelimiter);
        if (!Token.InListRange(nextToken, _parser.Tokens) ||
            !StringHelper.IsNumericString(_parser.Tokens[nextToken].Content))
        {
            return false;
        }

        SetAnimeSeason(token, _parser.Tokens[nextToken], _parser.Tokens[nextToken].Content);
        return true;

    }

    /// <summary>
    /// A Method to find the correct volume/episode number when prefixed (i.e. Vol.4).
    /// </summary>
    /// <param name="category">the category we're searching for</param>
    /// <param name="currentTokenPos">the current token position</param>
    /// <param name="token">the token</param>
    /// <returns>true if we found the volume/episode number</returns>
    public bool CheckExtentKeyword(ElementCategory category, int currentTokenPos, Token token)
    {
        var nToken = Token.FindNextToken(_parser.Tokens, currentTokenPos, Token.TokenFlag.FlagNotDelimiter);
        if (!IsTokenCategory(nToken, Token.TokenCategory.Unknown))
        {
            return false;
        }

        if (IndexOfFirstDigit(_parser.Tokens[nToken].Content) != 0)
        {
            return false;
        }

        switch (category)
        {
            case ElementCategory.EpisodeNumber:
                if (!_parser.ParseNumber.MatchEpisodePatterns(_parser.Tokens[nToken].Content, _parser.Tokens[nToken]))
                {
                    _parser.ParseNumber.SetEpisodeNumber(_parser.Tokens[nToken].Content, _parser.Tokens[nToken], false);
                }
                break;
            case ElementCategory.VolumeNumber:
                if (!_parser.ParseNumber.MatchVolumePatterns(_parser.Tokens[nToken].Content, _parser.Tokens[nToken]))
                {
                    _parser.ParseNumber.SetVolumeNumber(_parser.Tokens[nToken].Content, _parser.Tokens[nToken], false);
                }
                break;
        }

        token.Category = Token.TokenCategory.Identifier;
        return true;

    }


    public void BuildElement(ElementCategory category, bool keepDelimiters, List<Token> tokens)
    {
        var element = new StringBuilder();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Category)
            {
                case Token.TokenCategory.Unknown:
                    element.Append(token.Content);
                    token.Category = Token.TokenCategory.Identifier;
                    break;
                case Token.TokenCategory.Bracket:
                    element.Append(token.Content);
                    break;
                case Token.TokenCategory.Delimiter:
                    var delimiter = "";
                    if (!string.IsNullOrEmpty(token.Content))
                    {
                        delimiter = token.Content[0].ToString();
                    }

                    if (keepDelimiters)
                    {
                        element.Append(delimiter);
                    }
                    else if (Token.InListRange(i, tokens))
                    {
                        switch (delimiter)
                        {
                            case ",":
                            case "&":
                                element.Append(delimiter);
                                break;
                            default:
                                element.Append(' ');
                                break;
                        }
                    }
                    break;
            }
        }

        if (!keepDelimiters)
        {
            element = new StringBuilder(element.ToString().Trim(DashesWithSpace.ToCharArray()));
        }

        if (!string.IsNullOrEmpty(element.ToString()))
        {
            _parser.Elements.Add(new Element(category, element.ToString()));
        }
    }
}
