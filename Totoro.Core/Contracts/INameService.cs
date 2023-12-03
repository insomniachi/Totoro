namespace Totoro.Core.Contracts;

public interface INameService
{
    bool HasName(long id);
    string GetName(long id);
    void AddOrUpdate(long id, string name);
}
