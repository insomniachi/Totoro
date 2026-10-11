using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Settings;

namespace Totoro.Core.Modules.Anime;

public static class AnimeDatabasePreferences
{
	/// <summary>
	/// Preference key holding the id of the module that provides the anime database. A module provides its
	/// <see cref="IAnimeTracker"/> and <see cref="IAnimeMetadataProvider"/> as a pair, so this one key picks both.
	/// </summary>
	public const string Key = "SelectedAnimeDatabase";

	extension(IPreferences preferences)
	{
		/// <summary>
		/// Module id of the selected anime database; empty means the first one available. Changing it sends an
		/// <see cref="AnimeDatabaseChangedMessage"/>.
		/// </summary>
		public Guid SelectedAnimeDatabase
		{
			get => preferences.ReadKey<Guid>(Key);
			set
			{
				preferences.SaveKey(Key, value);
				WeakReferenceMessenger.Default.Send(new AnimeDatabaseChangedMessage(value));
			}
		}
	}
}

/// <summary>Sent (with WeakReferenceMessenger.Default) after the selected anime database changes.</summary>
public sealed record AnimeDatabaseChangedMessage(Guid ModuleId);
