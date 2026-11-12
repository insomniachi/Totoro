using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Serialization;

namespace Totoro.Core.Settings;

public class DataContainerConfig
{
	/// <summary>
	/// The settings to edit, bound to this object: setting a property's value sets this object's property. To edit
	/// without changing these settings until confirmed, edit a copy (IModuleSettings.CreateCopy, or the dialog, which
	/// edits copies of the properties).
	/// </summary>
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
						  // Its type is the property's, not its value's (a null one has none): it picks the editor.
						  .WithBinding(this, propertyInfo);

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
