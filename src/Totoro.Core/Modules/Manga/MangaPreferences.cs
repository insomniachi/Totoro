using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Settings;

namespace Totoro.Core.Modules.Manga;

// Settings store enums as numbers, so keep the order.
public enum ReaderMode
{
	/// <summary>No longer offered (the reader turns pages); read as <see cref="PagedLeftToRight"/>.</summary>
	[Description("Vertical (scroll)")] Vertical,
	[Description("Pages, left to right")] PagedLeftToRight,
	[Description("Pages, right to left")] PagedRightToLeft,
}

public static class MangaPreferences
{
	/// <summary>
	/// Preference key holding the id of the module whose <see cref="IMangaTracker"/> and <see cref="IMangaMetadataProvider"/>
	/// are the manga database; chosen apart from the anime database.
	/// </summary>
	public const string DatabaseKey = "SelectedMangaDatabase";

	/// <summary>Preference key holding the id of the module whose <see cref="IMangaProvider"/> chapters are read from.</summary>
	public const string ProviderKey = "SelectedMangaProvider";

	public const string ReaderModeKey = "ReaderMode";

	public const string ReaderTwoPagesKey = "ReaderTwoPages";

	extension(IPreferences preferences)
	{
		/// <summary>Module id of the manga database; empty means the first one available. Changing it sends a <see cref="MangaDatabaseChangedMessage"/>.</summary>
		public Guid SelectedMangaDatabase
		{
			get => preferences.ReadKey<Guid>(DatabaseKey);
			set
			{
				preferences.SaveKey(DatabaseKey, value);
				WeakReferenceMessenger.Default.Send(new MangaDatabaseChangedMessage(value));
			}
		}

		/// <summary>Module id of the source chapters are read from; empty means the first one available.</summary>
		public Guid SelectedMangaProvider
		{
			get => preferences.ReadKey<Guid>(ProviderKey);
			set => preferences.SaveKey(ProviderKey, value);
		}

		/// <summary>Which way the reader turns pages: left to right, or right to left as most manga read.</summary>
		public ReaderMode ReaderMode
		{
			get => preferences.ReadKey(ReaderModeKey, ReaderMode.PagedLeftToRight) is ReaderMode.PagedRightToLeft ? ReaderMode.PagedRightToLeft : ReaderMode.PagedLeftToRight;
			set => preferences.SaveKey(ReaderModeKey, value);
		}

		/// <summary>Whether the reader shows two pages side by side when it shows a page at a time.</summary>
		public bool ReaderTwoPages
		{
			get => preferences.ReadKey(ReaderTwoPagesKey, false);
			set => preferences.SaveKey(ReaderTwoPagesKey, value);
		}
	}
}

/// <summary>Sent (with WeakReferenceMessenger.Default) after the selected manga database changes.</summary>
public sealed record MangaDatabaseChangedMessage(Guid ModuleId);
