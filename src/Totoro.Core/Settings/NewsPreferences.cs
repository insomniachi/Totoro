using CommunityToolkit.Mvvm.Messaging;

namespace Totoro.Core.Settings;

/// <summary>Sent when News is shown in or hidden from the menu, so the menu follows straight away.</summary>
public sealed record ShowNewsChangedMessage(bool Show);

public static class NewsPreferences
{
	/// <summary>The News page's key in the menu (its label).</summary>
	public const string PageKey = "News";

	private const string ShowNewsKey = "ShowNews";

	extension(IPreferences preferences)
	{
		/// <summary>Whether the menu has News; on by default, for users who don't care for news to turn off.</summary>
		public bool ShowNews
		{
			get => preferences.ReadKey(ShowNewsKey, true);
			set
			{
				preferences.SaveKey(ShowNewsKey, value);
				WeakReferenceMessenger.Default.Send(new ShowNewsChangedMessage(value));
			}
		}
	}
}
