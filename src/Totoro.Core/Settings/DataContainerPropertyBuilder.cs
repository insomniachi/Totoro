using System.Drawing;
using System.Numerics;
using System.Reflection;

namespace Totoro.Core.Settings;

/// <summary>
/// Makes a <see cref="DataContainerProperty"/>. The order of the calls doesn't matter: the editor and allowed values
/// come from the type unless set, and setting one never resets another.
/// </summary>
public class DataContainerPropertyBuilder
{
	private IEnumerable<object>? _allowedValues;
	private string? _description;
	private string? _displayName;
	private string? _editorType;
	private string _name = "";
	private object? _value;
	private Type? _type;
	private string? _iconKey;
	private string? _parent;
	private double _minimum = double.MinValue;
	private double _maximum = double.MaxValue;
	private object? _target;
	private PropertyInfo? _property;

	public DataContainerPropertyBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	public DataContainerPropertyBuilder WithDisplayName(string displayName)
	{
		_displayName = displayName;
		return this;
	}

	public DataContainerPropertyBuilder WithDescription(string description)
	{
		_description = description;
		return this;
	}

	/// <summary>The value to hold; its type is the setting's unless <see cref="WithType"/> says otherwise.</summary>
	public DataContainerPropertyBuilder WithValue(object? value)
	{
		_value = value;
		return this;
	}

	/// <summary>
	/// The type the setting holds, e.g. for a value that's null: the editor for it (a date picker for a DateOnly...),
	/// what it converts values to, and whether it takes null (a nullable or reference type does).
	/// </summary>
	public DataContainerPropertyBuilder WithType(Type type)
	{
		_type = type;
		return this;
	}

	/// <summary>Reads and writes <paramref name="property"/> of <paramref name="target"/>, instead of holding a value.</summary>
	public DataContainerPropertyBuilder WithBinding(object target, PropertyInfo property)
	{
		_target = target;
		_property = property;
		return this;
	}

	public DataContainerPropertyBuilder WithAllowedValues(IEnumerable<object> allowedValues)
	{
		_allowedValues = allowedValues;
		return this;
	}

	public DataContainerPropertyBuilder WithAllowedValues<T>()
		where T : struct, Enum
	{
		return WithAllowedValues(Enum.GetValues<T>().Cast<object>());
	}

	public DataContainerPropertyBuilder WithEditorType(string type)
	{
		_editorType = type;
		return this;
	}

	public DataContainerPropertyBuilder WithParent(string name)
	{
		_parent = name;
		return this;
	}

	public DataContainerPropertyBuilder WithRange(double minimum, double maximum)
	{
		_minimum = minimum;
		_maximum = maximum;
		return this;
	}

	public DataContainerPropertyBuilder WithIcon(string key)
	{
		_iconKey = key;
		return this;
	}

	public DataContainerProperty Build()
	{
		var declared = _type ?? _property?.PropertyType ?? _value?.GetType();
		var type = declared is null ? null : Nullable.GetUnderlyingType(declared) ?? declared;
		var allowedValues = _allowedValues ?? DefaultAllowedValues(type);
		var target = _target;
		var property = _property;
		return new DataContainerProperty
		{
			Name = _name,
			DisplayName = string.IsNullOrEmpty(_displayName) ? _name : _displayName,
			Description = _description,
			Value = property is null ? _value : null,
			ValueType = type,
			AllowsNull = declared is null || !declared.IsValueType || Nullable.GetUnderlyingType(declared) is not null,
			// Choices are a combo box, whatever the type.
			EditorType = _editorType ?? (allowedValues.Any() ? DataContainerEditors.ComboBox : DefaultEditor(type)),
			AllowedValues = allowedValues,
			IconKey = _iconKey,
			Parent = _parent,
			Minimum = _minimum,
			Maximum = _maximum,
			Getter = property is null ? null : () => property.GetValue(target),
			Setter = property is { CanWrite: true } ? value => property.SetValue(target, value) : null,
		};
	}

	private static IEnumerable<object> DefaultAllowedValues(Type? type)
	{
		// A [Flags] one takes any combination, so its values aren't the only ones allowed.
		return type is { IsEnum: true } && !type.IsDefined(typeof(FlagsAttribute), false) ? Enum.GetValues(type).Cast<object>() : [];
	}

	private static string DefaultEditor(Type? type)
	{
		if (type is null)
		{
			return DataContainerEditors.TextBox;
		}

		if (type == typeof(bool))
		{
			return DataContainerEditors.ToggleSwitch;
		}

		if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumber<>)))
		{
			return DataContainerEditors.NumberBox;
		}

		if (type.IsEnum)
		{
			// One that isn't [Flags] has its values as allowed values, so it's a combo box.
			return DataContainerEditors.Flags;
		}

		if (type == typeof(DateOnly) || type == typeof(DateTime) || type == typeof(DateTimeOffset))
		{
			return DataContainerEditors.DatePicker;
		}

		if (type == typeof(TimeOnly))
		{
			return DataContainerEditors.TimePicker;
		}

		if (type == typeof(TimeSpan))
		{
			return DataContainerEditors.Duration;
		}

		if (type == typeof(Color))
		{
			return DataContainerEditors.Color;
		}

		return type != typeof(string) && typeof(IEnumerable<string>).IsAssignableFrom(type)
			? DataContainerEditors.Tokens
			: DataContainerEditors.TextBox;
	}
}
