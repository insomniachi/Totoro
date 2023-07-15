using CommunityToolkit.WinUI.Connectivity;

namespace Totoro.WinUI.Services;


internal class ConnectivityService : ReactiveObject, IConnectivityService
{
    [Reactive] public bool IsConnected { get; set; }

    public IObservable<Unit> ConnectionLost { get; }
    public IObservable<Unit> Connected { get; }

    public ConnectivityService()
    {
        IsConnected = NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable;
        ConnectionLost = this.ObservableForProperty(x => x.IsConnected, x => x).Where(x => !x).Select(_ => Unit.Default);
        Connected = this.ObservableForProperty(x => x.IsConnected, x => x).Where(x => x).Select(_ => Unit.Default);
        NetworkHelper.Instance.NetworkChanged += ConnectivityChanged;
    }

    private void ConnectivityChanged(object sender, EventArgs e)
    {
        IsConnected = NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable;
    }
}
