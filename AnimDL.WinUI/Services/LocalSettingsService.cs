using System;
using System.Collections.Generic;
using System.IO;
using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.Core.Contracts.Services;
using AnimDL.WinUI.Core.Helpers;
using AnimDL.WinUI.Helpers;
using AnimDL.WinUI.Models;
using Microsoft.Extensions.Options;
using Windows.Storage;

namespace AnimDL.WinUI.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string _defaultApplicationDataFolder = "AnimDL/ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";

    private readonly IFileService _fileService;
    private readonly LocalSettingsOptions _options;
    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;
    private IDictionary<string, object> _settings;
    private bool _isInitialized;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        _options = options.Value;
        _applicationDataFolder = Path.Combine(_localApplicationData, _options.ApplicationDataFolder ?? _defaultApplicationDataFolder);
        _localsettingsFile = _options.LocalSettingsFile ?? _defaultLocalSettingsFile;
        _settings = new Dictionary<string, object>();
        Initialize();
    }

    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }
        
        _settings = _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localsettingsFile);
        _isInitialized = true;
    }

    public T ReadSetting<T>(string key, T defaultValue = default)
    {
        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
            {
                return Json.ToObject<T>((string)obj);
            }
        }
        else
        {
            if (_settings != null && _settings.TryGetValue(key, out var obj))
            {
                return Json.ToObject<T>((string)obj);
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
            _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings);
        }
    }
}
