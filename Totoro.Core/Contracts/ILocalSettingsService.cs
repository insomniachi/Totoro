namespace Totoro.Core.Contracts;

public interface ILocalSettingsService
{
    T ReadSetting<T>(string key, T deafultValue = default);
    void SaveSetting<T>(string key, T value);
    void RemoveSetting(string key);
}

public static class LocalSettingsServiceExtensions
{
    public static T ReadSetting<T>(this ILocalSettingsService service, Key<T> key)
    {
        return service.ReadSetting(key.Name, key.Default.Value);
    }

    public static T ReadSetting<T>(this ILocalSettingsService service, Key<T> key, T defaultValue)
    {
        return service.ReadSetting(key.Name, defaultValue);
    }

    public static void SaveSetting<T>(this ILocalSettingsService service, Key<T> key, T value)
    {
        service.SaveSetting(key.Name, value);
    }
}
