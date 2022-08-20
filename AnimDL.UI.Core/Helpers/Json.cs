using System.Text.Json;

namespace AnimDL.UI.Core.Helpers;

public static class Json
{
    public static T ToObject<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value);
    }

    public static string Stringify(object value)
    {
        return JsonSerializer.Serialize(value);
    }
}
