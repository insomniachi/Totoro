using Akavache;

namespace Totoro.Core.Services;

public class LocalSettingsService : ILocalSettingsService
{
    public const string ApplicationName = @"Totoro";

    public LocalSettingsService()
    {
        Akavache.Registrations.Start(ApplicationName);
    }

    public string ApplicationDataFolder => "";

    public IObservable<T> ReadSetting<T>(string key, T defaultValue = default)
    {
        return BlobCache.LocalMachine.GetOrCreateObject<T>(key, () => defaultValue);
    }

    public void SaveSetting<T>(string key, T value)
    {
        BlobCache.LocalMachine.InsertObject(key, value);
    }

    public void RemoveSetting(string key)
    {
        BlobCache.LocalMachine.Invalidate(key);
    }
}
