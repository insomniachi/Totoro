using System.IO;
using Microsoft.Extensions.Options;
using Totoro.Core.Helpers;
using Totoro.WinUI.Helpers;
using Totoro.WinUI.Models;
using Windows.Storage;

namespace Totoro.WinUI.Services;

public class LegacyLocalSettingsService : ILegacyLocalSettingsService
{
    private const string _defaultApplicationDataFolder = "Totoro/ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";

    private readonly IFileService _fileService;
    private readonly LocalSettingsOptions _options;
    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;
    private IDictionary<string, object> _settings;
    private bool _isInitialized;

    public LegacyLocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        _options = options.Value;
        _applicationDataFolder = Path.Combine(_localApplicationData, _options.ApplicationDataFolder ?? _defaultApplicationDataFolder);
        _localsettingsFile = _options.LocalSettingsFile ?? _defaultLocalSettingsFile;

        if (!Directory.Exists(_applicationDataFolder))
        {
            Directory.CreateDirectory(_applicationDataFolder);
        }

        Initialize();
    }

    public string ApplicationDataFolder => _applicationDataFolder;

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _settings = _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localsettingsFile) ?? new Dictionary<string, object>();
        _isInitialized = true;
    }

    public IObservable<T> ReadSetting<T>(string key, T defaultValue = default)
    {
        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
            {
                try
                {
                    return Observable.Return(Json.ToObject<T>((string)obj));
                }
                catch
                {
                    return Observable.Return(defaultValue);
                }
            }
        }
        else
        {
            if (_settings != null && _settings.TryGetValue(key, out var obj))
            {
                try
                {
                    return Observable.Return(Json.ToObject<T>((string)obj));
                }
                catch
                {
                    return Observable.Return(defaultValue);
                }
            }
        }

        return Observable.Return(defaultValue);
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
            _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings);
        }
    }

    public bool ContainsKey(string key) => _settings.ContainsKey(key);

    public void RemoveSetting(string key) => throw new NotImplementedException();
}
