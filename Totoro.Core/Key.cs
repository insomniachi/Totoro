namespace Totoro.Core;


public interface IKey
{
    string Name { get; }
}

public class Key<T> : IKey
{
    public string Name { get; }
    public Lazy<T> Default { get; }

    public Key(string name, T defaultValue)
    {
        Name = name;
        Default = new Lazy<T>(defaultValue);
    }

    public Key(string name, Func<T> defaultValueFactory)
    {
        Name = name;
        Default = new Lazy<T>(defaultValueFactory);
    }

    public Key(string name)
    {
        Name = name;
        Default = new Lazy<T>();
    }
}
