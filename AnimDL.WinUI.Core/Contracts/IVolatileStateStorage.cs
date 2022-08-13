namespace AnimDL.WinUI.Core.Contracts;

public interface IVolatileStateStorage
{
    public IState GetState(Type vmType);
}
