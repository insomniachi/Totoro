using System.Text.RegularExpressions;

namespace Totoro.Plugins.Helpers;

public partial class RegexHelper
{
    [GeneratedRegex(@"(\d+)")]
    public static partial Regex IntegerRegex();
}
