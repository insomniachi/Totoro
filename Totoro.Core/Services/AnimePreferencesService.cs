namespace Totoro.Core.Services;


public class AnimePreferencesService : IAnimePreferencesService
{
    private readonly Dictionary<long,AnimePreferences> _maps;
    private readonly ISettings _settings;
    private readonly IKnownFolders _knownFolders;
    private readonly IFileService _fileService;
    private readonly string _fileName = @"anime-preferences.json";

    public AnimePreferencesService(ISettings settings,
                       IKnownFolders knownFolders,
                       IFileService fileService)
    {
        _settings = settings;
        _knownFolders = knownFolders;
        _fileService = fileService;

        _maps = fileService.Read<Dictionary<long, AnimePreferences>>(knownFolders.ApplicationData, _fileName) ?? [];
    }

    public void AddPreferences(long id, AnimePreferences preferences)
    {
        _maps.Add(id, preferences);
    }

    public AnimePreferences GetPreferences(long id)
    {
        return _maps[id];
    }

    public bool HasAlias(long id)
    {
        if (HasPreferences(id) == false)
        {
            return false;
        }

        return !string.IsNullOrEmpty(GetPreferences(id).Alias);
    }

    public bool HasPreferences(long id)
    {
        return _maps.ContainsKey(id);
    }

    public void Save()
    {
        _fileService.Save(_knownFolders.ApplicationData, _fileName, _maps);
    }
}


