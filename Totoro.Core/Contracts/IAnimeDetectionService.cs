namespace Totoro.Core.Contracts;

public interface IAnimeDetectionService
{
    Task<long?> DetectFromTitle(string title, bool useNotification = false);
    Task<long?> DetectFromFileName(string fileName, bool useNotification = false);
}
