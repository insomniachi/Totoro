namespace Totoro.Core.Contracts
{
    public interface ITorrentsService
    {
        ObservableCollection<TorrentModel> ActiveDownlaods { get; }

        Task Download(IDownloadableContent content);
    }
}