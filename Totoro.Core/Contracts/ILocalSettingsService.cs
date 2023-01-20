namespace Totoro.Core.Contracts;

public interface ILocalSettingsService
{
    string ApplicationDataFolder { get; }
    T ReadSetting<T>(string key, T deafultValue = default);
    string ReadSettingString(string key, string defaultValue = default);
    void SaveSetting<T>(string key, T value);
    bool ContainsKey(string key);
}
