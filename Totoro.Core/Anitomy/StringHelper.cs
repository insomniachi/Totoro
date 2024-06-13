
namespace Anitomy;


/// <summary>
/// A string helper class that is analogous to <code>string.cpp</code> of the original Anitomy, and <code>StringHelper.java</code> of AnitomyJ.
/// </summary>
public static class StringHelper
{

    /// <summary>
    /// Returns whether or not the character is alphanumeric
    /// </summary>
    public static bool IsAlphanumericChar(char c)
    {
        return c >= '0' && c <= '9' || c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';
    }

    /// <summary>
    /// Returns whether or not the character is a hex character.
    /// </summary>
    private static bool IsHexadecimalChar(char c)
    {
        return c >= '0' && c <= '9' || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f';
    }

    /// <summary>
    /// Returns whether or not the character is a latin character
    /// </summary>
    private static bool IsLatinChar(char c)
    {
        // We're just checking until the end of the Latin Extended-B block, 
        // rather than all the blocks that belong to the Latin script.
        return c <= '\u024F';
    }

    /// <summary>
    /// Returns whether or not the <code>str</code> is a hex string.
    /// </summary>
    public static bool IsHexadecimalString(string str)
    {
        return !string.IsNullOrEmpty(str) && str.All(IsHexadecimalChar);
    }

    /// <summary>
    /// Returns whether or not the <code>str</code> is mostly a latin string.
    /// </summary>
    public static bool IsMostlyLatinString(string str)
    {
        var length = !string.IsNullOrEmpty(str) ? 1.0 : str.Length;
        return str.Where(IsLatinChar).Count() / length >= 0.5;
    }

    /// <summary>
    /// Returns whether or not the <code>str</code> is a numeric string.
    /// </summary>
    public static bool IsNumericString(string str)
    {
        return str.All(char.IsDigit);
    }

    /// <summary>
    /// Returns the int value of the <code>str</code>; 0 otherwise.
    /// </summary>
    public static int StringToInt(string str)
    {
        try
        {
            return int.Parse(str);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 0;
        }
    }

    public static string SubstringWithCheck(string str, int start, int count)
    {
        if (start + count > str.Length)
        {
            count = str.Length - start;
        }

        return str.Substring(start, count);
    }
}

