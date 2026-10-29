using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Settings;

namespace Totoro.Core.Navigation;

/// <summary>What the app is showing: the main menu's pages are for one or the other (or both, like Settings).</summary>
public enum MediaMode
{
	Anime,
	Manga,
}

public static class MediaModePreferences
{
	private const string Key = "MediaMode";

	extension(IPreferences preferences)
	{
		/// <summary>The mode the app is in, kept for next time; changing it sends a <see cref="MediaModeChangedMessage"/>.</summary>
		public MediaMode MediaMode
		{
			get => preferences.ReadKey(Key, MediaMode.Anime);
			set
			{
				preferences.SaveKey(Key, value);
				WeakReferenceMessenger.Default.Send(new MediaModeChangedMessage(value));
			}
		}
	}
}

/// <summary>Sent (with WeakReferenceMessenger.Default) after the app switches between anime and manga.</summary>
public sealed record MediaModeChangedMessage(MediaMode Mode);
