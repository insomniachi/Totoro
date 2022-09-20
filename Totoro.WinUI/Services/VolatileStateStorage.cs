using Microsoft.Extensions.Caching.Memory;
using Totoro.Core;

namespace Totoro.WinUI.Services;

public class VolatileStateStorage : IVolatileStateStorage
{
    private readonly IMemoryCache _states;

    public VolatileStateStorage(IMemoryCache states)
    {
        _states = states;
    }

    public IState GetState(Type vmType)
    {
        return _states.GetOrCreate(vmType, entry =>
        {
            entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            return new State();
        });
    }
}
