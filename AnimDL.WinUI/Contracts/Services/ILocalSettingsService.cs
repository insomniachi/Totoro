using System.Runtime.CompilerServices;

namespace AnimDL.WinUI.Contracts.Services;

public interface ILocalSettingsService
{
    T ReadSetting<T>(string key, T deafultValue = default);
    void SaveSetting<T>(string key, T value);
}
