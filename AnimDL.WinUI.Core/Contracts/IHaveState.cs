using System.Runtime.CompilerServices;

namespace AnimDL.WinUI.Core.Contracts;

public interface IHaveState
{
    Task SetInitialState();
    void StoreState(IState state);
    void RestoreState(IState state);
}

public interface IState
{
    bool IsEmpty { get; }
    public TState GetValue<TState>(string name);
    void AddOrUpdate<TState>(TState value, [CallerArgumentExpression("value")] string propertyName = "");
}
