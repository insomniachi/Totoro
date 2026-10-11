using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Media;
using Totoro.Modules.MediaPlayers.Vlc;

namespace Totoro.ViewModels.Test;

/// <summary>
/// A playground for the built-in player: plays any video in it, with a made-up opening and ending marked, and shows
/// what it offers over the video (skipping, the next episode) without playing an episode to the end.
/// </summary>
public partial class BuiltInPlayerViewModel(IFactory<IMediaPlayer, Guid> players) : ObservableObject
{
	private readonly IInteractiveMediaPlayer? _player = players.Create(VlcModule.Id) as IInteractiveMediaPlayer;

	/// <summary>A file path or URL.</summary>
	[ObservableProperty]
	public partial string Source { get; set; } = "";

	/// <summary>What the last offer came to.</summary>
	[ObservableProperty]
	public partial string Result { get; set; } = "";

	[RelayCommand]
	private void Play()
	{
		if (_player is null || !Uri.TryCreate(Source.Trim('"', ' '), UriKind.Absolute, out var uri))
		{
			Result = "Enter a file path or URL.";
			return;
		}

		// Once its length is known: an opening from 0:10 to 1:40, and an ending in the last 1:30.
		void OnDuration(object? sender, TimeSpan duration)
		{
			_player.DurationChanged -= OnDuration;
			_ = _player.SetChaptersAsync(
			[
				new MediaChapter("Episode", TimeSpan.Zero),
				new MediaChapter(nameof(MediaSegmentType.Opening), TimeSpan.FromSeconds(10)),
				new MediaChapter("Episode", TimeSpan.FromSeconds(100)),
				new MediaChapter(nameof(MediaSegmentType.Ending), duration - TimeSpan.FromSeconds(90)),
			]);
		}

		_player.DurationChanged += OnDuration;
		_player.Play(new MediaItem(uri, "Test video"), TimeSpan.Zero);
		// Made-up episodes for its episode list, the second playing; picking one is reported here.
		_player.SetEpisodes(
		[
			new PlayerEpisode(1, "The first episode", "3 Oct 2025 · 24 min", "What happens in the first episode.", null, true, false, 0),
			new PlayerEpisode(2, "The second episode", "10 Oct 2025 · 24 min", "What happens in the second.", null, false, true, 0),
			new PlayerEpisode(3, "The third episode", "17 Oct 2025 · 24 min", "What happens in the third.", null, false, false, 0.4),
		]);
		_player.EpisodeRequested -= OnEpisodeRequested;
		_player.EpisodeRequested += OnEpisodeRequested;
		Result = "";
	}

	private void OnEpisodeRequested(object? sender, int number) => Result = $"Episode {number} was picked.";

	[RelayCommand]
	private async Task OfferSkipAsync()
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		Result = "Offered for 10 seconds…";
		Result = _player is not null && await _player.OfferAsync("Skip opening", timeout.Token) ? "Skip was clicked." : "Skip was taken away.";
	}

	[RelayCommand]
	private async Task OfferNextAsync()
	{
		Result = "Offered, with a countdown…";
		Result = _player is not null && await _player.OfferNextAsync("Episode 2", TimeSpan.FromSeconds(5), CancellationToken.None)
			? "The next episode was accepted."
			: "The next episode was declined.";
	}

	[RelayCommand]
	private async Task ShowStatusAsync()
	{
		_player?.ShowStatus("Getting episode 2…");
		await Task.Delay(TimeSpan.FromSeconds(3));
		_player?.ShowStatus(null);
	}

	[RelayCommand]
	private Task ShowMessage() => _player?.ShowMessageAsync("Skipped the opening", TimeSpan.FromSeconds(3)) ?? Task.CompletedTask;
}
