using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Windows.Networking.Connectivity;

namespace Totoro.UI.Networking;

/// <summary>
/// Whether the PC has internet access, for the title bar's offline icon; and <see cref="Restored"/> when it comes back,
/// so pages that failed to load can try again.
/// </summary>
/// <remarks>Create it (first use <see cref="Current"/>) on the UI thread: its changes are raised there.</remarks>
public sealed partial class NetworkStatus : ObservableObject
{
	public static NetworkStatus Current => field ??= new();

	private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

	private NetworkStatus()
	{
		Update();
		NetworkInformation.NetworkStatusChanged += _ => _dispatcher.TryEnqueue(Update);
	}

	/// <summary>Raised on the UI thread when the PC gets internet access back.</summary>
	public event EventHandler? Restored;

	[ObservableProperty]
	public partial bool IsOffline { get; private set; }

	private void Update()
	{
		var wasOffline = IsOffline;
		try
		{
			IsOffline = NetworkInformation.GetInternetConnectionProfile()?.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.InternetAccess;
		}
		catch
		{
			// Seen failing while adapters change; left as it was until the next change.
		}

		if (wasOffline && !IsOffline)
		{
			Restored?.Invoke(this, EventArgs.Empty);
		}
	}
}
