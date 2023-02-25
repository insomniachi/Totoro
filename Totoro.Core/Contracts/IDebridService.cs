using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Contracts;

public interface IDebridService
{
    DebridServiceType Type { get; }
    Task<bool> Check(string magneticLink);
    Task<IEnumerable<DirectDownloadLink>> GetDirectDownloadLinks(string magneticLink);
    Task<IEnumerable<Transfer>> GetTransfers();
    Task<string> CreateTransfer(string magneticLink);
    bool IsAuthenticated { get; }
}

public interface IDebridServiceContext
{
    Task<bool> Check(string magneticLink);
    Task<IEnumerable<DirectDownloadLink>> GetDirectDownloadLinks(string magneticLink);
    Task<IEnumerable<Transfer>> GetTransfers();
    Task<string> CreateTransfer(string magneticLink);
    bool IsAuthenticated { get; }
}

public interface IDebridServiceOptions
{
    ProviderOptions this[DebridServiceType type] { get; }
    void Save(DebridServiceType type);
    IObservable<DebridServiceType> Changed { get; }
}
