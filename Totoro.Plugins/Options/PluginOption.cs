using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Totoro.Plugins.Options;

#nullable disable

[DebuggerDisplay("{Value}")]
public class PluginOption : ReactiveObject
{
    public string Name { get; init; }
    public string DisplayName { get; init; }
    public string Description { get; init; }
    public string Glyph { get; set; }
    [Reactive] public string Value { get; set; }
}

public class PluginOptionBuilder
{
    private string _name;
    private string _displayName;
    private string _description;
    private string _glyph;
    private string _value;
    private IEnumerable<string> _allowedValues;

    public PluginOptionBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public PluginOptionBuilder WithDisplayName(string displayName)
    { 
        _displayName = displayName;
        return this;
    }

    public PluginOptionBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public PluginOptionBuilder WithGlyph(string glyph)
    {
        _glyph = glyph;
        return this;
    }

    public PluginOptionBuilder WithValue(string value)
    {
        _value = value;
        return this;
    }

    public PluginOptionBuilder WithValue<T>(T value)
    {
        _value = value.ToString();
        return this;
    }

    public PluginOptionBuilder WithNameAndValue<T>(T value, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        _value = value.ToString();
        _name = valueExpression.Split('.').LastOrDefault();
        _displayName = _name;
        return this;
    }

    public PluginOptionBuilder WithAllowedValues(IEnumerable<string> allowedValues)
    {
        _allowedValues = allowedValues;
        return this;
    }

    public PluginOptionBuilder WithAllowedValues<T>(IEnumerable<T> allowedValues)
    {
        _allowedValues = allowedValues.Select(x => x.ToString());
        return this;
    }

    public PluginOptionBuilder WithAllowedValues<T>()
        where T: struct, Enum
    {
        _allowedValues = Enum.GetNames<T>();
        return this;
    }

    public PluginOption ToPluginOption()
    {
        return new PluginOption
        {
            Name = _name,
            DisplayName = _displayName,
            Description = _description,
            Glyph = _glyph,
            Value = _value
        };
    }

    public SelectablePluginOption ToSelectablePluginOption()
    {
        return new SelectablePluginOption
        {
            Name = _name,
            DisplayName = _displayName,
            Description = _description,
            Glyph = _glyph,
            Value = _value,
            AllowedValues = _allowedValues
        };
    }
}