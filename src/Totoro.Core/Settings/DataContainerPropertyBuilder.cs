using System.Drawing;
using System.Numerics;

namespace Totoro.Core.Settings;

public class DataContainerPropertyBuilder
{
	private IEnumerable<object> _allowedValues = [];
	private string? _description;
	private string? _displayName;
	private string _editorType = DataContainerEditors.TextBox;
	private string _name = "";
	private object? _value = "";
	private Type? _valueType;
	private string? _iconKey;
	private string? _parent;
	private double _minimum = double.MinValue;
	private double _maximum = double.MaxValue;

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

	/// <summary>The value, and (unless it's null) its type, which picks the editor; see <see cref="WithType"/>.</summary>
	public DataContainerPropertyBuilder WithValue(object? value)
	{
		_value = value;
		if (value is not null)
		{
			WithType(value.GetType());
		}

		return this;
	}

	/// <summary>
	/// The type the setting holds (a nullable's underlying type), e.g. a property's when its value is null: the editor
	/// for it (a date picker for a DateOnly...) and what the editor writes back.
	/// </summary>
	public DataContainerPropertyBuilder WithType(Type type)
	{
		_valueType = Nullable.GetUnderlyingType(type) ?? type;
		SetDefaultsForType(_valueType);
		return this;
	}

	public DataContainerPropertyBuilder WithAllowedValues(IEnumerable<object> allowedValues)
	{
		_allowedValues = allowedValues;
		_editorType = DataContainerEditors.ComboBox;
		return this;
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

	public DataContainerPropertyBuilder WithAllowedValues<T>()
		where T : struct, Enum
	{
		_allowedValues = Enum.GetValues<T>().Cast<object>();
		_editorType = DataContainerEditors.ComboBox;
		return this;
	}

	public DataContainerProperty Build()
	{
		return new DataContainerProperty
		{
			Name = _name,
			DisplayName = string.IsNullOrEmpty(_displayName) ? _name : _displayName,
			Description = _description,
			Value = _value,
			ValueType = _valueType,
			EditorType = _editorType,
			AllowedValues = _allowedValues,
			IconKey = _iconKey,
			Parent = _parent,
			Minimum = _minimum,
			Maximum = _maximum,
		};
	}

	private void SetDefaultsForType(Type type)
	{
		_editorType = DataContainerEditors.TextBox;
		_allowedValues = [];
		if (type == typeof(bool))
		{
			_editorType = DataContainerEditors.ToggleSwitch;
		}
		else if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumber<>)))
		{
			_editorType = DataContainerEditors.NumberBox;
		}
		else if (type.IsEnum && type.IsDefined(typeof(FlagsAttribute), false))
		{
			// Any combination of the values: one choice each, not a list of allowed values.
			_editorType = DataContainerEditors.Flags;
		}
		else if (type.IsEnum)
		{
			_editorType = DataContainerEditors.ComboBox;
			_allowedValues = Enum.GetValues(type).Cast<object>();
		}
		else if (type == typeof(DateOnly) || type == typeof(DateTime) || type == typeof(DateTimeOffset))
		{
			_editorType = DataContainerEditors.DatePicker;
		}
		else if (type == typeof(TimeOnly))
		{
			_editorType = DataContainerEditors.TimePicker;
		}
		else if (type == typeof(TimeSpan))
		{
			_editorType = DataContainerEditors.Duration;
		}
		else if (type == typeof(Color))
		{
			_editorType = DataContainerEditors.Color;
		}
		else if (type != typeof(string) && typeof(IEnumerable<string>).IsAssignableFrom(type))
		{
			_editorType = DataContainerEditors.Tokens;
		}
	}
}
