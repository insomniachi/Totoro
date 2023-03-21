namespace Totoro.Core.Contracts;

public interface ILocalSettingsService
{
    string ApplicationDataFolder { get; }
    IObservable<T> ReadSetting<T>(string key, T deafultValue = default);
    void SaveSetting<T>(string key, T value);
    void RemoveSetting(string key);
}

public interface ILegacyLocalSettingsService : ILocalSettingsService
{

}

public static class LocalSettingsServiceExtensions
{
    public static IObservable<T> ReadSetting<T>(this ILocalSettingsService service, Key<T> key)
    {
        return service.ReadSetting(key.Name, key.Default.Value);
    }
}
