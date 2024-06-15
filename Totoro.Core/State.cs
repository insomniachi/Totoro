using System.Runtime.CompilerServices;

namespace Totoro.Core;

public class State : IState
{
    private readonly Dictionary<string, object> _state = [];

    public bool IsEmpty => _state.Count == 0;

    public TState GetValue<TState>(string name) => _state.TryGetValue(name, out object value) ? (TState)value : default;

    public void AddOrUpdate<TState>(TState value, [CallerArgumentExpression(nameof(value))] string propertyName = "")
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

