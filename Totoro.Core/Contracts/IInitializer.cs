namespace Totoro.Core.Contracts
{
    public interface IInitializer
    {
        Task Initialize();
        void ShutDown();
    }
}
