using System.Text;

namespace Anitomy;

/// <summary>
/// A class that will tokenize an anime filename.
/// </summary>
/// <remarks>
/// Tokenize a filename into <see cref="Element"/>s
/// </remarks>
/// <param name="filename">the filename</param>
/// <param name="elements">the list of elements where pre-identified tokens will be added</param>
/// <param name="options">the parser options</param>
/// <param name="tokens">the list of tokens where tokens will be added</param>
public class Tokenizer(string filename, List<Element> elements, Options options, List<Token> tokens)
{
    private readonly string _filename = filename;
    private readonly List<Element> _elements = elements;
    private readonly Options _options = options;
    private readonly List<Token> _tokens = tokens;
    private static readonly List<Tuple<string, string>> Brackets =
    [
      new("(", ")"), // U+0028-U+0029
      new Tuple<string, string>("[", "]"), // U+005B-U+005D Square bracket
      new Tuple<string, string>("{", "}"), // U+007B-U+007D Curly bracket
      new Tuple<string, string>("\u300C", "\u300D"),  // Corner bracket
      new Tuple<string, string>("\u300E", "\u300E"),  // White corner bracket
      new Tuple<string, string>("\u3010", "\u3011"), // Black lenticular bracket
      new Tuple<string, string>("\uFF08", "\uFF09") // Fullwidth parenthesis
    ];

    /// <summary>
    /// Returns true if tokenization was successful; false otherwise.
    /// </summary>
    /// <returns></returns>
    public bool Tokenize()
    {
        TokenizeByBrackets();
        return _tokens.Count > 0;
    }

    /// <summary>
    /// Adds a token to the internal list of tokens
    /// </summary>
    /// <param name="category">the token category</param>
    /// <param name="enclosed">whether or not the token is enclosed in braces</param>
    /// <param name="range">the token range</param>
    private void AddToken(Token.TokenCategory category, bool enclosed, TokenRange range)
    {
        _tokens.Add(new Token(category, StringHelper.SubstringWithCheck(_filename, range.Offset, range.Size), enclosed));
    }

    private string GetDelimiters(TokenRange range)
    {
        var delimiters = new StringBuilder();

        bool IsDelimiter(char c)
        {
            return !StringHelper.IsAlphanumericChar(c) && _options.AllowedDelimiters.Contains(c.ToString()) && !delimiters.ToString().Contains(c.ToString());
        }

        foreach (var i in Enumerable.Range(range.Offset, Math.Min(_filename.Length, range.Offset + range.Size) - range.Offset)
          .Where(value => IsDelimiter(_filename[value])))
        {
            delimiters.Append(_filename[i]);
        }

        return delimiters.ToString();
    }

    /// <summary>
    /// Tokenize by bracket.
    /// </summary>
    private void TokenizeByBrackets()
    {
        string matchingBracket = null;

        int FindFirstBracket(int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                foreach (var bracket in Brackets)
                {
                    if (!_filename[i].Equals(char.Parse(bracket.Item1)))
                        continue;
                    matchingBracket = bracket.Item2;
                    return i;
                }
            }

            return -1;
        }

        var isBracketOpen = false;
        for (var i = 0; i < _filename.Length;)
        {
            var foundIdx = !isBracketOpen ? FindFirstBracket(i, _filename.Length) : _filename.IndexOf(matchingBracket, i, StringComparison.Ordinal);

            var range = new TokenRange(i, foundIdx == -1 ? _filename.Length : foundIdx - i);
            if (range.Size > 0)
            {
                // Check if our range contains any known anime identifiers
                TokenizeByPreidentified(isBracketOpen, range);
            }

            if (foundIdx != -1)
            {
                // mark as bracket
                AddToken(Token.TokenCategory.Bracket, true, new TokenRange(range.Offset + range.Size, 1));
                isBracketOpen = !isBracketOpen;
                i = foundIdx + 1;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Tokenize by looking for known anime identifiers
    /// </summary>
    /// <param name="enclosed">whether or not the current <code>range</code> is enclosed in braces</param>
    /// <param name="range">the token range</param>
    private void TokenizeByPreidentified(bool enclosed, TokenRange range)
    {
        var preidentifiedTokens = new List<TokenRange>();

        // Find known anime identifiers
        KeywordManager.PeekAndAdd(_filename, range, _elements, preidentifiedTokens);

        var offset = range.Offset;
        var subRange = new TokenRange(range.Offset, 0);
        while (offset < range.Offset + range.Size)
        {
            foreach (var preidentifiedToken in preidentifiedTokens)
            {
                if (offset != preidentifiedToken.Offset)
                {
                    continue;
                }

                if (subRange.Size > 0)
                {
                    TokenizeByDelimiters(enclosed, subRange);
                }

                AddToken(Token.TokenCategory.Identifier, enclosed, preidentifiedToken);
                subRange.Offset = preidentifiedToken.Offset + preidentifiedToken.Size;
                offset = subRange.Offset - 1; // It's going to be incremented below
            }

            subRange.Size = ++offset - subRange.Offset;
        }

        // Either there was no preidentified token range, or we're now about to process the tail of our current range
        if (subRange.Size > 0)
        {
            TokenizeByDelimiters(enclosed, subRange);
        }
    }

    /// <summary>
    /// Tokenize by delimiters allowed in <see cref="Options"/>.AllowedDelimiters.
    /// </summary>
    /// <param name="enclosed">whether or not the current <code>range</code> is enclosed in braces</param>
    /// <param name="range">the token range</param>
    private void TokenizeByDelimiters(bool enclosed, TokenRange range)
    {
        var delimiters = GetDelimiters(range);

        if (string.IsNullOrEmpty(delimiters))
        {
            AddToken(Token.TokenCategory.Unknown, enclosed, range);
            return;
        }

        for (int i = range.Offset, end = range.Offset + range.Size; i < end;)
        {
            var found = Enumerable.Range(i, Math.Min(end, _filename.Length) - i)
              .Where(c => delimiters.Contains(_filename[c].ToString()))
              .DefaultIfEmpty(end)
              .FirstOrDefault();

            var subRange = new TokenRange(i, found - i);
            if (subRange.Size > 0)
            {
                AddToken(Token.TokenCategory.Unknown, enclosed, subRange);
            }

            if (found != end)
            {
                AddToken(Token.TokenCategory.Delimiter, enclosed, new TokenRange(subRange.Offset + subRange.Size, 1));
                i = found + 1;
            }
            else
            {
                break;
            }
        }

        ValidateDelimiterTokens();
    }

    /// <summary>
    /// Validates tokens (make sure certain words delimited by certain tokens aren't split)
    /// </summary>
    private void ValidateDelimiterTokens()
    {
        bool IsDelimiterToken(int it)
        {
            return Token.InListRange(it, _tokens) && _tokens[it].Category == Token.TokenCategory.Delimiter;
        }

        bool IsUnknownToken(int it)
        {
            return Token.InListRange(it, _tokens) && _tokens[it].Category == Token.TokenCategory.Unknown;
        }

        bool IsSingleCharacterToken(int it)
        {
            return IsUnknownToken(it) && _tokens[it].Content.Length == 1 && _tokens[it].Content[0] != '-';
        }

        void AppendTokenTo(Token src, Token dest)
        {
            dest.Content += src.Content;
            src.Category = Token.TokenCategory.Invalid;
        }

        for (var i = 0; i < _tokens.Count; i++)
        {
            var token = _tokens[i];
            if (token.Category != Token.TokenCategory.Delimiter)
                continue;
            var delimiter = token.Content[0];

            var prevToken = Token.FindPrevToken(_tokens, i, Token.TokenFlag.FlagValid);
            var nextToken = Token.FindNextToken(_tokens, i, Token.TokenFlag.FlagValid);

            // Check for single-character tokens to prevent splitting group names,
            // keywords, episode numbers, etc.
            if (delimiter != ' ' && delimiter != '_')
            {

                // Single character token
                if (IsSingleCharacterToken(prevToken))
                {
                    AppendTokenTo(token, _tokens[prevToken]);

                    while (IsUnknownToken(nextToken))
                    {
                        AppendTokenTo(_tokens[nextToken], _tokens[prevToken]);

                        nextToken = Token.FindNextToken(_tokens, i, Token.TokenFlag.FlagValid);
                        if (!IsDelimiterToken(nextToken) || _tokens[nextToken].Content[0] != delimiter)
                            continue;
                        AppendTokenTo(_tokens[nextToken], _tokens[prevToken]);
                        nextToken = Token.FindNextToken(_tokens, nextToken, Token.TokenFlag.FlagValid);
                    }

                    continue;
                }

                if (IsSingleCharacterToken(nextToken))
                {
                    AppendTokenTo(token, _tokens[prevToken]);
                    AppendTokenTo(_tokens[nextToken], _tokens[prevToken]);
                    continue;
                }
            }

            // Check for adjacent delimiters
            if (IsUnknownToken(prevToken) && IsDelimiterToken(nextToken))
            {
                var nextDelimiter = _tokens[nextToken].Content[0];
                if (delimiter != nextDelimiter && delimiter != ',')
                {
                    if (nextDelimiter == ' ' || nextDelimiter == '_')
                    {
                        AppendTokenTo(token, _tokens[prevToken]);
                    }
                }
            }
            else if (IsDelimiterToken(prevToken) && IsDelimiterToken(nextToken))
            {
                var prevDelimiter = _tokens[prevToken].Content[0];
                var nextDelimiter = _tokens[nextToken].Content[0];
                if (prevDelimiter == nextDelimiter && prevDelimiter != delimiter)
                {
                    token.Category = Token.TokenCategory.Unknown; // e.g. "& in "_&_"
                }
            }

            // Check for other special cases
            if (delimiter != '&' && delimiter != '+')
            {
                continue;
            }

            if (!IsUnknownToken(prevToken) || !IsUnknownToken(nextToken))
            {
                continue;
            }

            if (!StringHelper.IsNumericString(_tokens[prevToken].Content)
                || !StringHelper.IsNumericString(_tokens[nextToken].Content))
            {
                continue;
            }

            AppendTokenTo(token, _tokens[prevToken]);
            AppendTokenTo(_tokens[nextToken], _tokens[prevToken]); // e.g. 01+02
        }

        // Remove invalid tokens
        _tokens.RemoveAll(token => token.Category == Token.TokenCategory.Invalid);
    }
}

