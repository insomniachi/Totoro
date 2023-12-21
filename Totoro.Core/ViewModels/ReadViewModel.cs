using Totoro.Plugins.Contracts;
using Totoro.Plugins.Manga;
using Totoro.Plugins.Manga.Contracts;

namespace Totoro.Core.ViewModels;

public class ReadViewModel : NavigatableViewModel
{
    private readonly ISettings _settings;
    private readonly IPluginFactory<MangaProvider> _providerFactory;
    private MangaProvider _provider;

    public ReadViewModel(ISettings settings,
                         IPluginFactory<MangaProvider> providerFactory)
    {
        _settings = settings;
        _providerFactory = providerFactory;

        this.WhenAnyValue(x => x.SelectedChapter)
            .WhereNotNull()
            .SelectMany(chapter => _provider.ChapterProvider.GetImages(chapter).ToListAsync().AsTask())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(images =>
            {
                Pages.Clear();
                NumberOfPages = images.Count;
                SelectedPage = 0;
            })
            .Subscribe(images => Pages.AddRange(images));

    }

    [Reactive] public List<ChapterModel> Chapters { get; set; }
    [Reactive] public ChapterModel SelectedChapter { get; set; }
    [Reactive] public int NumberOfPages { get; set; }
    [Reactive] public int SelectedPage { get; set; }
    public ObservableCollection<string> Pages { get; } = [];


    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        var providerType = parameters.GetValueOrDefault("ProviderType", _settings.DefaultMangaProviderType) as string;
        _provider = _providerFactory.CreatePlugin(providerType);

        if (parameters.ContainsKey("SearchResult"))
        {
            var result = (ICatalogItem)parameters["SearchResult"];
            Chapters = await _provider.ChapterProvider.GetChapters(result.Url).ToListAsync();
        }
    }
}
