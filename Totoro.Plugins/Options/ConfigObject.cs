using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Serialization;

namespace Totoro.Plugins.Options;

public abstract class ConfigObject
{
    public void UpdateValues(PluginOptions options)
    {
        var type = GetType();
        foreach (var option in options)
        {
            var propInfo = type.GetProperty(option.Name);
            var currentValue = propInfo!.GetValue(this);
            var optionValue = GetValue(options, option.Name, propInfo.PropertyType, currentValue);
            if(optionValue is not null)
            {
                propInfo.SetValue(this, optionValue);
            }
        }
    }

    public PluginOptions ToPluginOptions()
    {
        var options = new PluginOptions();
        foreach (var propertyInfo in GetType().GetProperties())
        {
            if(propertyInfo.GetCustomAttribute<IgnoreDataMemberAttribute>() is { })
            {
                continue;
            }

            var builder = new PluginOptionBuilder()
                .WithName(propertyInfo.Name)
                .WithDisplayName(propertyInfo.Name)
                .WithValue(propertyInfo.GetValue(this));

            if(propertyInfo.PropertyType.IsEnum)
            {
                builder.WithAllowedValues(Enum.GetNames(propertyInfo.PropertyType));
            }

            if(propertyInfo.GetCustomAttribute<DescriptionAttribute>() is { } descriptionAttribute)
            {
                builder.WithDescription(descriptionAttribute.Description);
            }

            if(propertyInfo.GetCustomAttribute<DisplayNameAttribute>() is { } displayNameAttribute)
            {
                builder.WithDisplayName(displayNameAttribute.DisplayName);
            }

            if(propertyInfo.GetCustomAttribute<GlyphAttribute>() is { } glyphAttribute)
            {
                builder.WithGlyph(glyphAttribute.Glyph);
            }

            if(propertyInfo.GetCustomAttribute<AllowedValuesAttribute>() is { } allowedValuesAttribute)
            {
                builder.WithAllowedValues(allowedValuesAttribute.Values);
            }

            var option = builder.HasAllowedValues()
                ? builder.ToSelectablePluginOption()
                : builder.ToPluginOption();

            options.Add(option);
        }

        return options;
    }

    protected virtual object? GetValue(PluginOptions options, string name, Type t, object? defaultValue)
    {
        if(t == typeof(int))
        {
            return options.GetInt32(name, (int)defaultValue!);
        }
        else if(t == typeof(double))
        {
            return options.GetDouble(name, (double)defaultValue!);
        }
        else if(t == typeof(string))
        {
            return options.GetString(name, (string)defaultValue!);
        }
        else if(t.IsEnum)
        {
            return options.GetEnum(t, name, defaultValue!);
        }

        return null;
    }
}

public class ConfigManager<TConfig>
    where TConfig : ConfigObject, new()
{
    public static TConfig Current { get; } = Default;
    public static TConfig Default => new();
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class GlyphAttribute(string glyph) : Attribute
{
    public string Glyph { get; } = glyph;
}
