using Totoro.Core.Helpers;
using Totoro.WinUI.Helpers;
using Windows.Storage;

namespace Totoro.WinUI.Services;

public class LegacyLocalSettingsService : ILegacyLocalSettingsService
{

    private readonly IFileService _fileService;
    private readonly IKnownFolders _knownFolders;
    private IDictionary<string, object> _settings;
    private bool _isInitialized;

    public LegacyLocalSettingsService(IFileService fileService,
                                      IKnownFolders knownFolders)
    {
        _fileService = fileService;
        _knownFolders = knownFolders;
        Initialize();
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _settings = _fileService.Read<IDictionary<string, object>>(_knownFolders.ApplicationData, "LocalSettings.json") ?? new Dictionary<string, object>();
        _isInitialized = true;
    }

    public T ReadSetting<T>(string key, T defaultValue = default)
    {
        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
            {
                try
                {
                    return Json.ToObject<T>((string)obj);
                }
                catch
                {
                    return defaultValue;
                }
            }
        }
        else
        {
            if (_settings != null && _settings.TryGetValue(key, out var obj))
            {
                try
                {
                    return Json.ToObject<T>((string)obj);
                }
                catch
                {
                    return defaultValue;
                }
            }
        }

        return defaultValue;
    }

    public void SaveSetting<T>(string key, T value)
    {
        if (RuntimeHelper.IsMSIX)
        {
            ApplicationData.Current.LocalSettings.Values[key] = Json.Stringify(value);
        }
        else
        {
            _settings[key] = Json.Stringify(value);
            _fileService.Save(_knownFolders.ApplicationData, "LocalSettings.json", _settings);
        }
    }

    public bool ContainsKey(string key) => _settings.ContainsKey(key);

    public void RemoveSetting(string key) => _settings.Remove(key);
}
