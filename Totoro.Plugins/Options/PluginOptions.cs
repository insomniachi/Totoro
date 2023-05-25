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

    public PluginOptions AddSelectableOption(string name, string displayName, string value, IEnumerable<string> options)
    {
        Add(new SelectablePluginOption
        {
            Name = name,
            DisplayName = displayName,
            Value = value,
            AllowedValues = options
        });

        return this;
    }

    public PluginOptions AddSelectableOption<T>(string name, string displayName, T value, IEnumerable<T>? options = null)
        where T : struct, Enum
    {
        Add(new SelectablePluginOption
        {
            Name = name,
            DisplayName = displayName,
            Value = value.ToString(),
            AllowedValues = (options ?? Enum.GetValues<T>()).Select(x => Enum.GetName(x)!)
        });

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
    public TEnum GetEnum<TEnum>(string name, TEnum defaultValue) where TEnum: Enum => GetValue(name, defaultValue, x => (TEnum)Enum.Parse(typeof(TEnum), x));

    private T GetValue<T>(string name, T defaultValue, Func<string, T> parser)
    {
        if (this.FirstOrDefault(x => x.Name == name) is not { } option)
        {
            return defaultValue;
        }

        if(string.IsNullOrEmpty(option.Value))
        {
            return defaultValue;
        }

        return parser(option.Value);
    }
}