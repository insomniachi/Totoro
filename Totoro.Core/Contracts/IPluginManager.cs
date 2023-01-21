namespace Totoro.Core.Contracts;

public interface IPluginManager
{
    Task Initialize();
    void SaveConfig();
    void SaveConfig(string provider, IParameters config);
}
