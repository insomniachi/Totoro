using System.Reactive.Subjects;

namespace Totoro.Core.Services;

internal class DebridServiceOptions : IDebridServiceOptions
{
    private readonly Dictionary<DebridServiceType, ProviderOptions> _options;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly Subject<DebridServiceType> _subject = new();

    public DebridServiceOptions(ILocalSettingsService localSettingsService)
    {
        _options = localSettingsService.ReadSetting<Dictionary<DebridServiceType, ProviderOptions>>("DebridOptions", GetDefault()) ?? GetDefault();
        _localSettingsService = localSettingsService;
    }

    public ProviderOptions this[DebridServiceType type] => _options[type];

    public IObservable<DebridServiceType> Changed => _subject;

    public void Save(DebridServiceType type)
    {
        _localSettingsService.SaveSetting("DebridOptions", _options);
        _subject.OnNext(type);
    }

    private static Dictionary<DebridServiceType, ProviderOptions> GetDefault()
    {
        return new()
        {
            [DebridServiceType.Premiumize] = new ProviderOptions().AddOption("Key", "API key", "")
        };
    }

}
