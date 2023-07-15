namespace Totoro.Core.Contracts;

public interface IConnectivityService
{
    bool IsConnected { get; }
    IObservable<Unit> ConnectionLost { get; }
    IObservable<Unit> Connected { get; }
}
