namespace Totoro.Plugins.Contracts.Optional;

public interface IHaveSeason : IHaveYear
{
    string Season { get; }
}
