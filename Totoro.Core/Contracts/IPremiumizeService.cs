
using Totoro.Core.Services;

namespace Totoro.Core.Contracts;

public interface IPremiumizeService
{
    Task<bool> Check(string magneticLink);
    void SetApiKey(string apiKey);
    Task<IEnumerable<DirectDownloadLink>> GetDirectDownloadLinks(string magneticLink);
    Task<IEnumerable<Transfer>> GetTransfers();
    Task<string> CreateTransfer(string magneticLink);
    bool IsAuthenticated { get; }
}
