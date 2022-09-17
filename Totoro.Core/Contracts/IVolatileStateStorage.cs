namespace Totoro.Core.Contracts;

public interface IVolatileStateStorage
{
    public IState GetState(Type vmType);
}
