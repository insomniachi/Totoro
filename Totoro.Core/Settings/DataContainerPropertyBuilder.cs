using System.Numerics;
using System.Runtime.CompilerServices;

namespace Totoro.Core.Settings;

public class DataContainerPropertyBuilder
{
	private IEnumerable<object> _allowedValues = [];
	private string? _description;
	private string? _displayName;
	private string _editorType = "TextBox";
	private string _name = "";
	private object? _value = "";
	private string? _iconKey = "";

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

	public DataContainerPropertyBuilder WithValue(object? value)
	{
		_value = value;
		SetDefaultsForType(value);
		return this;
	}

	public DataContainerPropertyBuilder WithValueAndName<T>(T value, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
	{
		_value = value;
		_name = valueExpression.Split('.').LastOrDefault() ?? "";
		_displayName = _name;
		SetDefaultsForType(value);
		return this;
	}

	public DataContainerPropertyBuilder WithAllowedValues(IEnumerable<object> allowedValues)
	{
		_allowedValues = allowedValues;
		_editorType = "ComboBox";
		return this;
	}

	public DataContainerPropertyBuilder WithEditorType(string type)
	{
		_editorType = type;
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
		_editorType = "ComboBox";
		return this;
	}

	public bool HasAllowedValues()
	{
		return _allowedValues.Any();
	}

	public DataContainerProperty Build()
	{
		var item = new DataContainerProperty
		{
			Name = _name,
			DisplayName = string.IsNullOrEmpty(_displayName) ? _name : _displayName,
			Description = _description,
			Value = _value,
			EditorType = _editorType,
			AllowedValues = _allowedValues,
			IconKey = _iconKey
		};

		Reset();

		return item;
	}

	private void Reset()
	{
		_name = "";
		_displayName = "";
		_displayName = "";
		_value = "";
		_editorType = "TextBox";
		_allowedValues = [];
	}

	private void SetDefaultsForType(object? value)
	{
		if (value is null)
		{
			return;
		}

		var type = value.GetType();
		if (type.GetInterfaces()
				.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumber<>)))
		{
			_editorType = "NumberBox";
		}
		else if (type.IsEnum)
		{
			_editorType = "ComboBox";
			_allowedValues = Enum.GetValues(type).Cast<object>();
		}
	}
}