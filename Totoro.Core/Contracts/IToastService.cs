namespace Totoro.Core.Contracts
{
    public interface IToastService
    {
        void DownloadCompleted(string directory, string name);
        public void CheckEpisodeComplete(AnimeModel anime, int currentEp);
        void Playing(AnimeModel anime, string episode);
        void PromptAnimeSelection(IEnumerable<AnimeModel> items, AnimeModel defaultSelection);
    }
}
