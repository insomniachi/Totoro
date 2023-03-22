namespace Totoro.Core;

internal class KnownFolders : IKnownFolders
{
    public string ApplicationData { get; }
    public string ApplicationDataLegacy { get; }
    public string Updates { get; }
    public string Plugins { get; }
    public string Logs { get; }
    public string Torrents { get; }

    public KnownFolders()
    {
        ApplicationDataLegacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Totoro/ApplicationData");
        ApplicationData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Totoro");
        Updates = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Totoro/Updates");
        Plugins = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Totoro/Plugins");
        Logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Totoro/Logs");
        Torrents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Totoro/Torrents");
    }
}

