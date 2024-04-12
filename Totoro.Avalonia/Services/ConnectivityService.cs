using System;
using System.Reactive;
using System.Reactive.Subjects;
using Totoro.Core.Contracts;

namespace Totoro.Avalonia.Services;

public class ConnectivityService : IConnectivityService
{
    public bool IsConnected { get; } = true;
    public IObservable<Unit> ConnectionLost { get; } = new Subject<Unit>();
    public IObservable<Unit> Connected { get; } = new Subject<Unit>();
}