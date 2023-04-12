namespace Totoro.Core.Contracts;

public interface IRssDownloader
{
    Task Initialize();
    void SaveState();
}