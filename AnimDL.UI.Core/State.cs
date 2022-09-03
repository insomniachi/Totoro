using System.Runtime.CompilerServices;

namespace AnimDL.UI.Core;

public class State : IState
{
    private readonly Dictionary<string, object> _state = new();

    public bool IsEmpty => !_state.Any();

    public TState GetValue<TState>(string name) => _state.ContainsKey(name) ? (TState)_state[name] : default;

    public void AddOrUpdate<TState>(TState value, [CallerArgumentExpression("value")] string propertyName = "")
    {
        if (!_state.ContainsKey(propertyName))
        {
            _state.Add(propertyName, value);
        }
        else
        {
            _state[propertyName] = value;
        }
    }
}

