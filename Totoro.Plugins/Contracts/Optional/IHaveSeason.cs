namespace Totoro.Plugins.Contracts.Optional;

public interface IHaveSeason : IHaveYear
{
    public string Season { get; }
}
