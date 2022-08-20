namespace AnimDL.UI.Core.Contracts;

public interface ILocalSettingsService
{
    T ReadSetting<T>(string key, T deafultValue = default);
    void SaveSetting<T>(string key, T value);
}
