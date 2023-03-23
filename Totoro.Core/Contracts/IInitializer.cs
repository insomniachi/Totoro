namespace Totoro.Core.Contracts
{
    public interface IInitializer
    {
        Task Initialize();
        Task ShutDown();
        IObservable<Unit> OnShutDown { get; }
    }
}
