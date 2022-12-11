namespace Totoro.Core.Contracts;

public interface ILocalSettingsService
{
    string ApplicationDataFolder { get; }
    T ReadSetting<T>(string key, T deafultValue = default);
    void SaveSetting<T>(string key, T value);
}
