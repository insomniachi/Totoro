namespace Totoro.Core.Contracts
{
    public interface IKnownFolders
    {
        string ApplicationData { get; }
        string ApplicationDataLegacy { get; }
        string Updates { get; }
        string Plugins { get; }
        string Logs { get; }
    }
}
