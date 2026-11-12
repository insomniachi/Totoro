using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Serialization;

namespace Totoro.Core.Settings;

public class DataContainerConfig
{
	public void UpdateValues(List<DataContainerProperty> options)
	{
		var type = GetType();
		foreach (var option in options)
		{
			if (type.GetProperty(option.Name) is not { CanWrite: true } propInfo)
			{
				continue;
			}

			// The editors keep each value's own type, except the text box, which any other type (e.g. TimeSpan) falls back to.
			var value = option.Value is string text && propInfo.PropertyType != typeof(string) ? FromText(text, propInfo.PropertyType) : option.Value;

			// A value of another type (or null) wasn't edited, or isn't valid: keep the current one.
			if (propInfo.PropertyType.IsInstanceOfType(value))
			{
				propInfo.SetValue(this, value);
			}
		}
	}

	private static object? FromText(string text, Type type)
	{
		try
		{
			return TypeDescriptor.GetConverter(type).ConvertFromInvariantString(text);
		}
		catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException || ex.InnerException is FormatException or OverflowException)
		{
			return null;
		}
	}

	public DataContainer ToDataContainer()
	{
		var options = new DataContainer();
		foreach (var propertyInfo in GetType().GetProperties())
		{
			if (propertyInfo.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null)
			{
				continue;
			}

			var builder = new DataContainerPropertyBuilder()
						  .WithName(propertyInfo.Name)
						  .WithDisplayName(propertyInfo.Name)
						  .WithValue(propertyInfo.GetValue(this))
						  // The property's type, not the value's: a null one has none. It picks the editor, and an enum's values
						  // are offered as values, not names, so the editor's choice converts back to the property's type.
						  .WithType(propertyInfo.PropertyType);

			if (propertyInfo.GetCustomAttribute<DescriptionAttribute>() is { } descriptionAttribute)
			{
				builder.WithDescription(descriptionAttribute.Description);
			}

			if (propertyInfo.GetCustomAttribute<DisplayNameAttribute>() is { } displayNameAttribute)
			{
				builder.WithDisplayName(displayNameAttribute.DisplayName);
			}

			if (propertyInfo.GetCustomAttribute<AllowedValuesAttribute>() is { } allowedValuesAttribute)
			{
				builder.WithAllowedValues(allowedValuesAttribute.Values.OfType<object>());
			}

			if (propertyInfo.GetCustomAttribute<RangeAttribute>() is { } rangeAttribute)
			{
				builder.WithRange(Convert.ToDouble(rangeAttribute.Minimum), Convert.ToDouble(rangeAttribute.Maximum));
			}

			if (propertyInfo.GetCustomAttribute<IconAttribute>() is { } iconAttribute)
			{
				builder.WithIcon(iconAttribute.IconKey);
			}

			if (propertyInfo.GetCustomAttribute<ParentAttribute>() is { } parentAttribute)
			{
				builder.WithParent(parentAttribute.Name);
			}

			if (propertyInfo.GetCustomAttribute<EditorAttribute>() is { } editorTypeAttribute)
			{
				builder.WithEditorType(editorTypeAttribute.EditorTypeName);
			}

			ConfigureProperty(builder, propertyInfo);

			options.Add(builder.Build());
		}

		return options;
	}

	protected virtual void ConfigureProperty(DataContainerPropertyBuilder propertyBuilder, PropertyInfo info) { }
}
