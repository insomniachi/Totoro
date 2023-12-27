using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData.Binding;

namespace Totoro.Plugins.Options;

public class PluginOptions : Collection<PluginOption>
{
    public PluginOptions AddOption(Func<PluginOptionBuilder, PluginOption> creator)
    {
        var builder = new PluginOptionBuilder();
        Add(creator(builder));
        return this;
    }

    public bool TrySetValue(string name, string value)
    {
        if (this.FirstOrDefault(x => x.Name == name) is not { } option)
        {
            return false;
        }
        option.Value = value;
        return true;
    }

    public IObservable<PluginOption> WhenChanged() => Observable.Merge(this.Select(x => x.WhenAnyPropertyChanged(nameof(PluginOption.Value))))!;
    public string GetString(string name, string defaultValue) => this.FirstOrDefault(x => x.Name == name)?.Value ?? defaultValue;
    public int GetInt32(string name, int defaultValue) => GetValue(name, defaultValue, int.Parse);
    public double GetDouble(string name, double defaultValue) => GetValue(name, defaultValue, double.Parse);
    public TEnum GetEnum<TEnum>(string name, TEnum defaultValue) where TEnum : Enum => GetValue(name, defaultValue, x => (TEnum)Enum.Parse(typeof(TEnum), x));

    public T GetValue<T>(string name, T defaultValue, Func<string, T> parser)
    {
        if (this.FirstOrDefault(x => x.Name == name) is not { } option)
        {
            return defaultValue;
        }

        if (string.IsNullOrEmpty(option.Value))
        {
            return defaultValue;
        }

        try
        {
            return parser(option.Value);
        }
        catch
        {
            return defaultValue;
        }
    }

}