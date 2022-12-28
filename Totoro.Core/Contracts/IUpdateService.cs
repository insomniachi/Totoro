namespace Totoro.Core.Contracts
{
    public interface IUpdateService
    {
        IObservable<VersionInfo> OnUpdateAvailable { get; }
        Task<VersionInfo> DownloadUpdate(VersionInfo versionInfo);
        void InstallUpdate(VersionInfo versionInfo);
    }

    public class VersionInfo
    {
        public Version Version { get; set; }
        public string Details { get; set; }
        public string Url { get; set; }
        public string FilePath { get; set; }
    }
}
