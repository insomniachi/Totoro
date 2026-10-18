using Totoro.Core.Modules.Media;

namespace Totoro.Core.Settings;

/// <summary>What to do when an opening or ending starts.</summary>
public enum SkipMode
{
	/// <summary>Nothing; it plays.</summary>
	Off,

	/// <summary>Offer to skip it: a button in the built-in player, a hint to press the next-chapter key in others.</summary>
	Ask,

	/// <summary>Skip it.</summary>
	Automatic,
}

public static class PlaybackPreferences
{
	public const string OpeningSkipKey = "OpeningSkipMode";
	public const string EndingSkipKey = "EndingSkipMode";
	public const string PlayNextKey = "PlayNextAutomatically";
	public const string ShowOnDiscordKey = "ShowOnDiscord";

	/// <summary>Preference key holding the id of the anime provider episodes are found on (unless an anime has its own).</summary>
	public const string AnimeProviderKey = "SelectedAnimeProvider";

	/// <summary>Preference key holding the id of the module whose media player plays episodes.</summary>
	public const string MediaPlayerKey = "SelectedMediaPlayer";
	public const string VolumeKey = "PlayerVolume";
	public const string AspectRatioKey = "PlayerAspectRatio";

	extension(IPreferences preferences)
	{
		public SkipMode OpeningSkipMode
		{
			get => preferences.ReadKey(OpeningSkipKey, SkipMode.Ask);
			set => preferences.SaveKey(OpeningSkipKey, value);
		}

		public SkipMode EndingSkipMode
		{
			get => preferences.ReadKey(EndingSkipKey, SkipMode.Ask);
			set => preferences.SaveKey(EndingSkipKey, value);
		}

		/// <summary>Whether the next episode starts by itself (after a short countdown) when one is finished.</summary>
		public bool PlayNextAutomatically
		{
			get => preferences.ReadKey(PlayNextKey, false);
			set => preferences.SaveKey(PlayNextKey, value);
		}

		/// <summary>Whether the episode playing shows as the user's activity on Discord.</summary>
		public bool ShowOnDiscord
		{
			get => preferences.ReadKey(ShowOnDiscordKey, true);
			set => preferences.SaveKey(ShowOnDiscordKey, value);
		}

		/// <summary>Id of the anime provider episodes are found on, unless an anime has its own; empty means the first one available.</summary>
		public Guid SelectedAnimeProvider
		{
			get => preferences.ReadKey<Guid>(AnimeProviderKey);
			set => preferences.SaveKey(AnimeProviderKey, value);
		}

		/// <summary>Module id of the media player episodes play in; empty means the first one available.</summary>
		public Guid SelectedMediaPlayer
		{
			get => preferences.ReadKey<Guid>(MediaPlayerKey);
			set => preferences.SaveKey(MediaPlayerKey, value);
		}

		/// <summary>The built-in player's volume, from 0 to 100, kept from one episode to the next.</summary>
		public int PlayerVolume
		{
			get => preferences.ReadKey(VolumeKey, 100);
			set => preferences.SaveKey(VolumeKey, value);
		}

		/// <summary>How video is scaled or stretched in the built-in player.</summary>
		public PlayerAspectRatio PlayerAspectRatio
		{
			get => preferences.ReadKey(AspectRatioKey, PlayerAspectRatio.Fit);
			set => preferences.SaveKey(AspectRatioKey, value);
		}
	}
}
