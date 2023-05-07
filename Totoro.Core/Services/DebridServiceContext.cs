using System.Reactive.Subjects;
using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Services;

internal class DebridServiceContext : IDebridServiceContext
{
    private readonly Dictionary<DebridServiceType, IDebridService> _services;
    private readonly ISettings _settings;
    private readonly Subject<string> _onNewTransfer = new();


    public DebridServiceContext(IEnumerable<IDebridService> debridServices,
                                ISettings settings)
    {
        _services = debridServices.ToDictionary(x => x.Type, x => x);
        _settings = settings;
    }

    public bool IsAuthenticated => _services[_settings.DebridServiceType].IsAuthenticated;

    public IObservable<string> TransferCreated => _onNewTransfer;

    public Task<bool> Check(string magnetLink) => _services[_settings.DebridServiceType].Check(magnetLink);

    public IAsyncEnumerable<bool> Check(IEnumerable<string> magnetLinks) => _services[_settings.DebridServiceType].Check(magnetLinks);

    public async Task<string> CreateTransfer(string magnetLink)
    {
        var id = await _services[_settings.DebridServiceType].CreateTransfer(magnetLink);
        _onNewTransfer.OnNext(id);
        return id;
    }

    public Task<IEnumerable<DirectDownloadLink>> GetDirectDownloadLinks(string magnetLink) => _services[_settings.DebridServiceType].GetDirectDownloadLinks(magnetLink);

    public Task<IEnumerable<Transfer>> GetTransfers() => _services[_settings.DebridServiceType].GetTransfers();
}
