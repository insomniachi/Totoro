using System.Reactive.Subjects;
using System.ServiceModel.Syndication;
using System.Xml;
using Splat;

namespace Totoro.Core.Services.AnimixPlay;

public class AnimixPlayNotifier : IAiredEpisodeNotifier, IEnableLogger
{
	public const string Url = "https://animixplay.to/rsssub.xml";
	private List<AiredEpisode> _previousState = new();
	private readonly Subject<AiredEpisode> _onNewEpisode = new();

	public IObservable<AiredEpisode> OnNewEpisode => _onNewEpisode;

    public AnimixPlayNotifier()
	{
		Observable
			.Timer(TimeSpan.Zero, TimeSpan.FromMinutes(1))
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(_ =>
			{
				try
				{
					Run();
				}
				catch(Exception ex)
				{
					this.Log().Error(ex);
				}
			});
	}

	private void Run()
	{
		using var reader = XmlReader.Create(Url);
		var feed = SyndicationFeed.Load(reader);
		
		var items = feed.Items.Select(x => new AiredEpisode
		{
			TimeOfAiring = x.PublishDate.LocalDateTime,
			Anime = x.Title.Text,
			EpisodeUrl = x.Links.FirstOrDefault()?.Uri.ToString(),
			Id = x.ElementExtensions[0].GetObject<long>(),
			InfoText = x.ElementExtensions[1].GetObject<string>(),
		}).ToList();

		items.RemoveAll(_previousState.Contains);
		_previousState = items;

		foreach (var item in items)
		{
			_onNewEpisode.OnNext(item);
		}
    }
}

