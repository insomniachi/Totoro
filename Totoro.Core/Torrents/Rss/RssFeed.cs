
using System.Diagnostics;
using System.Reactive.Subjects;
using System.ServiceModel.Syndication;
using System.Xml;

namespace Totoro.Core.Torrents.Rss;

public class RssFeed
{
    private static readonly HttpClient _httpClient = new();
    private readonly Subject<RssFeedItem> _onNew = new();
    private IDisposable _disposable;

    public RssFeedOptions Options { get; }
    public List<RssFeedItem> Current { get; private set; } = [];
    public IObservable<RssFeedItem> OnNew => _onNew;

    public RssFeed(RssFeedOptions options)
    {
        Options = options;
    }

    public void Start()
    {
        _disposable?.Dispose();
        _disposable = Observable
            .Timer(TimeSpan.Zero, TimeSpan.FromMinutes(30))
            .SelectMany(_ => Fetch())
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);
    }

    public void Stop()
    {
        _disposable?.Dispose();
        _disposable = null;
    }

    public async Task<Unit> Fetch()
    {
        if (string.IsNullOrEmpty(Options.Url))
        {
            return Unit.Default;
        }

        if (Options.IsEnabled == false)
        {
            return Unit.Default;
        }

        var response = await _httpClient.GetAsync(Options.Url);

        if (!response.IsSuccessStatusCode)
        {
            return Unit.Default;
        }

        var content = await response.Content.ReadAsStreamAsync();
        var xmlReader = XmlReader.Create(content);
        var syndicationFeed = SyndicationFeed.Load(xmlReader);
        xmlReader.Close();

        var items = syndicationFeed.Items.Select(x => SyndicationItemConverter.Convert(Options.Url, x)).Where(x => x is not null).ToList();

        foreach (var item in items.Except(Current))
        {
            _onNew.OnNext(item);
        }

        Current = items;
        return Unit.Default;
    }
}

public class RssFeedOptions
{
    required public string Url { get; init; }
    public bool IsEnabled { get; set; }
    public static RssFeedOptions Default { get; } = new() { Url = "" };
}

[DebuggerDisplay("[{Subber}] {Tittle} ({Resolution})")]
public class TorrentNameFilter : ReactiveObject
{
    [Reactive] public string Title { get; set; } = "";
    [Reactive] public string Resolution { get; set; } = "";
    [Reactive] public string Subber { get; set; } = "";
}

[DebuggerDisplay("{Title}")]
public class RssFeedItem
{
    required public string Title { get; init; }
}

public class TorrentRssFeedItem : RssFeedItem
{
    required public string Torrent { get; init; }
}

public class MagnetRssFeedItem : RssFeedItem
{
    required public string Magnet { get; init; }
}
