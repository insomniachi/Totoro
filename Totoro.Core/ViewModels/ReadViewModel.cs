using Totoro.Plugins;
using Totoro.Plugins.Manga;
using Totoro.Plugins.Manga.Contracts;

namespace Totoro.Core.ViewModels;

public class ReadViewModel : NavigatableViewModel
{
    private readonly MangaProvider _provider;

    public ReadViewModel()
    {
        _provider = PluginFactory<MangaProvider>.Instance.CreatePlugin("manga-dex");

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
    public ObservableCollection<string> Pages { get; } = new();


    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if(parameters.ContainsKey("SearchResult"))
        {
            var result = (ICatalogItem)parameters["SearchResult"];
            Chapters = await _provider.ChapterProvider.GetChapters(result.Url).ToListAsync();
        }

    }
}
