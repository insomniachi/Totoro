namespace Totoro.Plugins.Contracts;

public interface IPluginManager
{
    Task Initialize(string folder);
}