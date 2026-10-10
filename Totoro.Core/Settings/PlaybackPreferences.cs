namespace Totoro.Core.Settings;

/// <summary>What to do when an opening or ending starts.</summary>
public enum SkipMode
{
	/// <summary>Nothing; it plays.</summary>
	Off,

	/// <summary>Say it can be skipped (with the player's next-chapter key).</summary>
	Ask,

	/// <summary>Skip it.</summary>
	Automatic,
}

public static class PlaybackPreferences
{
	public const string OpeningSkipKey = "OpeningSkipMode";
	public const string EndingSkipKey = "EndingSkipMode";
	public const string PlayNextKey = "PlayNextAutomatically";

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
	}
}
