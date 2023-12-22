namespace Totoro.Core.Services;
using ListServiceToNameMap = Dictionary<ListServiceType, Dictionary<long, string>>;

public class NameService : INameService
{
    private readonly ListServiceToNameMap _maps;
    private readonly ISettings _settings;
    private readonly IKnownFolders _knownFolders;
    private readonly IFileService _fileService;
    private readonly string _fileName = @"names.json";

    public NameService(ISettings settings,
                       IKnownFolders knownFolders,
                       IFileService fileService)
    {
        _settings = settings;
        _knownFolders = knownFolders;
        _fileService = fileService;
        _maps = fileService.Read<ListServiceToNameMap>(knownFolders.ApplicationData, _fileName) ?? [];
    }

    public void AddOrUpdate(long id, string name)
    {
        if (!_maps.TryGetValue(_settings.DefaultListService, out _))
        {
            _maps[_settings.DefaultListService] = [];
        }

        _maps[_settings.DefaultListService][id] = name;
        Save();
    }

    public string GetName(long id)
    {
        if (!_maps.TryGetValue(_settings.DefaultListService, out var map))
        {
            return string.Empty;
        }

        return map[id];
    }

    public bool HasName(long id)
    {
        if (!_maps.TryGetValue(_settings.DefaultListService, out var map))
        {
            return false;
        }

        return map.ContainsKey(id);
    }

    private void Save()
    {
        _fileService.Save(_knownFolders.ApplicationData, _fileName, _maps);
    }
}
