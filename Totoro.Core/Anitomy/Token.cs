namespace Anitomy;


/// <summary>
/// An anime filename is tokenized into individual <see cref="Token"/>s. This class represents an individual token.
/// </summary>
/// <remarks>
/// Constructs a new token
/// </remarks>
/// <param name="category">the token category</param>
/// <param name="content">the token content</param>
/// <param name="enclosed">whether or not the token is enclosed in braces</param>
public class Token(Token.TokenCategory category, string content, bool enclosed)
{
    /// <summary>
    /// The category of the token.
    /// </summary>
    public enum TokenCategory
    {
        Unknown,
        Bracket,
        Delimiter,
        Identifier,
        Invalid
    }

    /// <summary>
    /// TokenFlag, used for searching specific token categories. This allows granular searching of TokenCategories.
    /// </summary>
    public enum TokenFlag
    {
        // None
        FlagNone,

        // Categories
        FlagBracket, FlagNotBracket,
        FlagDelimiter, FlagNotDelimiter,
        FlagIdentifier, FlagNotIdentifier,
        FlagUnknown, FlagNotUnknown,
        FlagValid, FlagNotValid,

        // Enclosed (Meaning that it is enclosed in some bracket (e.g. [ ] ))
        FlagEnclosed, FlagNotEnclosed
    }

    /// <summary>
    /// Set of token category flags
    /// </summary>
    private static readonly List<TokenFlag> FlagMaskCategories =
    [
      TokenFlag.FlagBracket, TokenFlag.FlagNotBracket,
      TokenFlag.FlagDelimiter, TokenFlag.FlagNotDelimiter,
      TokenFlag.FlagIdentifier, TokenFlag.FlagNotIdentifier,
      TokenFlag.FlagUnknown, TokenFlag.FlagNotUnknown,
      TokenFlag.FlagValid, TokenFlag.FlagNotValid
    ];

    /// <summary>
    /// Set of token enclosed flags
    /// </summary>
    private static readonly List<TokenFlag> FlagMaskEnclosed =
    [
      TokenFlag.FlagEnclosed, TokenFlag.FlagNotEnclosed
    ];

    public TokenCategory Category { get; set; } = category;
    public string Content { get; set; } = content;
    public bool Enclosed { get; } = enclosed;

    /// <summary>
    /// Validates a token against the <code>flags</code>. The <code>flags</code> is used as a search parameter.
    /// </summary>
    /// <param name="token">the token</param>
    /// <param name="flags">the flags the token must conform against</param>
    /// <returns>true if the token conforms to the set of <code>flags</code>; false otherwise</returns>
    private static bool CheckTokenFlags(Token token, ICollection<TokenFlag> flags)
    {
        // Simple alias to check if flag is a part of the set
        bool CheckFlag(TokenFlag flag)
        {
            return flags.Contains(flag);
        }

        // Make sure token is the correct closure
        if (flags.Any(f => FlagMaskEnclosed.Contains(f)))
        {
            var success = CheckFlag(TokenFlag.FlagEnclosed) == token.Enclosed;
            if (!success)
            {
                return false; // Not enclosed correctly (e.g. enclosed when we're looking for non-enclosed).
            }
        }

        // Make sure token is the correct category
        if (!flags.Any(f => FlagMaskCategories.Contains(f)))
        {
            return true;
        }

        var secondarySuccess = false;

        void CheckCategory(TokenFlag fe, TokenFlag fn, TokenCategory c)
        {
            if (secondarySuccess)
                return;
            var result = CheckFlag(fe) ? token.Category == c : CheckFlag(fn) && token.Category != c;
            secondarySuccess = result;
        }

        CheckCategory(TokenFlag.FlagBracket, TokenFlag.FlagNotBracket, TokenCategory.Bracket);
        CheckCategory(TokenFlag.FlagDelimiter, TokenFlag.FlagNotDelimiter, TokenCategory.Delimiter);
        CheckCategory(TokenFlag.FlagIdentifier, TokenFlag.FlagNotIdentifier, TokenCategory.Identifier);
        CheckCategory(TokenFlag.FlagUnknown, TokenFlag.FlagNotUnknown, TokenCategory.Unknown);
        CheckCategory(TokenFlag.FlagNotValid, TokenFlag.FlagValid, TokenCategory.Invalid);
        return secondarySuccess;
    }

    /// <summary>
    /// Given a list of <code>tokens</code>, searches for any token token that matches the list of <code>flags</code>.
    /// </summary>
    /// <param name="tokens">the list of tokens</param>
    /// <param name="begin">the search starting position.</param>
    /// <param name="end">the search ending position.</param>
    /// <param name="flags">the search flags</param>
    /// <returns>the search result</returns>
    public static int FindToken(List<Token> tokens, int begin, int end, params TokenFlag[] flags)
    {
        return FindTokenBase(tokens, begin, end, i => i < tokens.Count, i => i + 1, flags);
    }

    /// <summary>
    /// Given a list of <code>tokens</code>, searches for the next token in <code>tokens</code> that matches the list of <code>flags</code>.
    /// </summary>
    /// <param name="tokens">the list of tokens</param>
    /// <param name="first">the search starting position.</param>
    /// <param name="flags">the search flags</param>
    /// <returns>the search result</returns>
    public static int FindNextToken(List<Token> tokens, int first, params TokenFlag[] flags)
    {
        return FindTokenBase(tokens, first + 1, tokens.Count, i => i < tokens.Count, i => i + 1, flags);
    }

    /// <summary>
    /// Given a list of <code>tokens</code>, searches for the previous token in <code>tokens</code> that matches the list of <code>flags</code>.
    /// </summary>
    /// <param name="tokens">the list of tokens</param>
    /// <param name="begin">the search starting position. Exclusive of position.Pos</param>
    /// <param name="flags">the search flags</param>
    /// <returns>the search result</returns>
    public static int FindPrevToken(List<Token> tokens, int begin, params TokenFlag[] flags)
    {
        return FindTokenBase(tokens, begin - 1, -1, i => i >= 0, i => i - 1, flags);
    }

    /// <summary>
    /// Given a list of tokens finds the first token that passes <see cref="CheckTokenFlags"/>.
    /// </summary>
    /// <param name="tokens">the list of the tokens to search</param>
    /// <param name="begin">the start index of the search.</param>
    /// <param name="end">the end index of the search.</param>
    /// <param name="shouldContinue">a function that returns whether or not we should continue searching</param>
    /// <param name="next">a function that returns the next search index</param>
    /// <param name="flags">the flags that each token should be validated against</param>
    /// <returns>the found token</returns>
    private static int FindTokenBase(
      List<Token> tokens,
      int begin,
      int end,
      Func<int, bool> shouldContinue,
      Func<int, int> next,
      params TokenFlag[] flags)
    {
        var find = new List<TokenFlag>();
        find.AddRange(flags);

        for (var i = begin; shouldContinue(i); i = next(i))
        {
            var token = tokens[i];
            if (CheckTokenFlags(token, find))
            {
                return i;
            }
        }

        return end;
    }

    public static bool InListRange(int pos, List<Token> list)
    {
        return -1 < pos && pos < list.Count;
    }

    public override bool Equals(object o)
    {
        if (this == o)
        {
            return true;
        }

        if (o is not Token)
        {
            return false;
        }

        var token = (Token)o;
        return Enclosed == token.Enclosed && Category == token.Category && Equals(Content, token.Content);
    }

    public override int GetHashCode()
    {
        var hashCode = -1776802967;
        hashCode = hashCode * -1521134295 + Category.GetHashCode();
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Content);
        hashCode = hashCode * -1521134295 + Enclosed.GetHashCode();
        return hashCode;
    }

    public override string ToString()
    {
        return $"Token{{category={Category}, content='{Content}', enclosed={Enclosed}}}";
    }
}
