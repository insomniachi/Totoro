namespace Totoro.Core.Contracts
{
    public interface IAnimeSoundsService
    {
        IList<AnimeSound> GetThemes(long id);
    }
}
