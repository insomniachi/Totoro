namespace Totoro.Core.Contracts;

public interface IAnimePreferencesService
{
    bool HasAlias(long id);
    bool HasPreferences(long id);
    AnimePreferences GetPreferences(long id);
    void AddPreferences(long id, AnimePreferences preferences);
    void Save();
}
