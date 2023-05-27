using System.Text;

namespace Totoro.Plugins.Helpers;

public static class EncodingHelper
{
    public static string ToBase64String(this string str) => Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
    public static string FromBase64String(this string str) => Encoding.UTF8.GetString(Convert.FromBase64String(str));
}
