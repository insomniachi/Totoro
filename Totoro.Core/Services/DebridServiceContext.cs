using System.Reactive.Subjects;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Services;

internal class DebridServiceContext : IDebridServiceContext
{
    private readonly Dictionary<DebridServiceType, Lazy<IDebridService>> _services;
    private readonly ISettings _settings;
    private readonly Subject<string> _onNewTransfer = new();


    public DebridServiceContext([FromKeyedServices(DebridServiceType.Premiumize)] Lazy<IDebridService> premiumizeService,
                                [FromKeyedServices(DebridServiceType.RealDebrid)] Lazy<IDebridService> realDebridService,
                                ISettings settings)
    {
        _services = new Dictionary<DebridServiceType, Lazy<IDebridService>>
        {
            { DebridServiceType.Premiumize, premiumizeService },
            { DebridServiceType.RealDebrid, realDebridService }
        };
        _settings = settings;
    }

    public bool IsAuthenticated => _services[_settings.DebridServiceType].Value.IsAuthenticated;

    public IObservable<string> TransferCreated => _onNewTransfer;

    public Task<bool> Check(string magnetLink) => _services[_settings.DebridServiceType].Value.Check(magnetLink);

    public IAsyncEnumerable<bool> Check(IEnumerable<string> magnetLinks) => _services[_settings.DebridServiceType].Value.Check(magnetLinks);

    public async Task<string> CreateTransfer(string magnetLink)
    {
        var id = await _services[_settings.DebridServiceType].Value.CreateTransfer(magnetLink);
        _onNewTransfer.OnNext(id);
        return id;
    }

    public Task<IEnumerable<DirectDownloadLink>> GetDirectDownloadLinks(string magnetLink) => _services[_settings.DebridServiceType].Value.GetDirectDownloadLinks(magnetLink);

    public Task<IEnumerable<Transfer>> GetTransfers() => _services[_settings.DebridServiceType].Value.GetTransfers();
}
