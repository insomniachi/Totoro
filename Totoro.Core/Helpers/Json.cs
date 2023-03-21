namespace Totoro.Core.Helpers;

public static class Json
{
    public static T ToObject<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value);
    }

    public static string Stringify(object value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(value, value.GetType());
    }
}