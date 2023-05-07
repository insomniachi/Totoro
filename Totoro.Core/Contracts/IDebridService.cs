using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Contracts;

public interface IDebridService
{
    DebridServiceType Type { get; }
    Task<bool> Check(string magnetLink);
    IAsyncEnumerable<bool> Check(IEnumerable<string> magnetLinks);
    Task<IEnumerable<DirectDownloadLink>> GetDirectDownloadLinks(string magnetLink);
    Task<IEnumerable<Transfer>> GetTransfers();
    Task<string> CreateTransfer(string magnetLink);
    bool IsAuthenticated { get; }
}

public interface IDebridServiceContext
{
    Task<bool> Check(string magnetLink);
    IAsyncEnumerable<bool> Check(IEnumerable<string> magnetLinks);
    Task<IEnumerable<DirectDownloadLink>> GetDirectDownloadLinks(string magnetLink);
    Task<IEnumerable<Transfer>> GetTransfers();
    Task<string> CreateTransfer(string magnetLink);
    IObservable<string> TransferCreated { get; }
    bool IsAuthenticated { get; }
}
