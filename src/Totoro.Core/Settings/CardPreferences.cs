using CommunityToolkit.Mvvm.Messaging;

namespace Totoro.Core.Settings;

/// <summary>What anime cards show (with the overlays a page gives them).</summary>
public static class CardPreferences
{
	public const string ShowNextEpisodeKey = "ShowNextEpisodeOnCards";

	extension(IPreferences preferences)
	{
		/// <summary>
		/// Whether cards of anime being watched or planned show how long until their next episode airs. Changing it sends
		/// a <see cref="CardPreferencesChangedMessage"/>.
		/// </summary>
		public bool ShowNextEpisodeOnCards
		{
			get => preferences.ReadKey(ShowNextEpisodeKey, true);
			set
			{
				preferences.SaveKey(ShowNextEpisodeKey, value);
				WeakReferenceMessenger.Default.Send(new CardPreferencesChangedMessage());
			}
		}
	}
}

/// <summary>Sent (with WeakReferenceMessenger.Default) after what anime cards show changes.</summary>
public sealed record CardPreferencesChangedMessage;
