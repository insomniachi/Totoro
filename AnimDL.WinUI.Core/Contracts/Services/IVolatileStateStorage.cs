namespace AnimDL.WinUI.Core.Contracts.Services;

public interface IVolatileStateStorage
{
    public IState GetState(Type vmType);
}
