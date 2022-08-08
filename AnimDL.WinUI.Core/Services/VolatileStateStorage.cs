using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Core.Contracts.Services;

namespace AnimDL.WinUI.Core.Services;

public class VolatileStateStorage : IVolatileStateStorage
{
    private readonly Dictionary<Type, State> _states = new();

    public IState GetState(Type vmType)
    {
        if(_states.ContainsKey(vmType) == false)
        {
            var state = new State();
            _states.Add(vmType, state);
            return state;
        }

        return _states[vmType];
    }
}
