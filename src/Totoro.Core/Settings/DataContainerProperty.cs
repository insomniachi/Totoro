using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.Core.Settings;

/// <summary>
/// A setting to edit: what it is (name, type, range, allowed values, editor) and its value. The value is either held
/// here (a property made on the fly, e.g. for a dialog asking for a few values), or a settings object's property, read
/// and written through (from <see cref="DataContainerConfig.ToDataContainer"/>).
/// </summary>
public partial class DataContainerProperty : ObservableObject
{
	private object? _value;

	public string Name { get; init; } = "";
	public string? DisplayName { get; init; }
	public string? Description { get; init; }
	public string EditorType { get; init; } = DataContainerEditors.TextBox;

	/// <summary>
	/// The value: the settings object's, if bound to one. Set it to a value of <see cref="ValueType"/> (a bound one
	/// throws on another); <see cref="TrySetValue"/> converts and checks it first.
	/// </summary>
	public object? Value
	{
		get => Getter is null ? _value : Getter();
		set
		{
			if (Setter is not null)
			{
				Setter(value);
			}
			else if (Getter is not null)
			{
				throw new InvalidOperationException($"{Header} can't be changed.");
			}
			else
			{
				_value = value;
			}

			OnPropertyChanged();
		}
	}

	/// <summary>The type the setting holds (a nullable's underlying type); null if unknown (then go by the value's own).</summary>
	public Type? ValueType { get; init; }

	/// <summary>Whether null is a value it can take: a nullable or a reference type, or a type that's unknown.</summary>
	public bool AllowsNull { get; init; } = true;

	public IEnumerable<object>? AllowedValues { get; init; }
	public string? IconKey { get; init; }

	/// <summary>The name of the property this one is shown under (see <see cref="ParentAttribute"/>); null for none.</summary>
	public string? Parent { get; init; }

	/// <summary>The lowest and highest a number may be (see RangeAttribute); unbounded by default.</summary>
	public double Minimum { get; init; } = double.MinValue;
	public double Maximum { get; init; } = double.MaxValue;

	/// <summary>Reads a settings object's property, for one bound to it (see the builder's WithBinding).</summary>
	internal Func<object?>? Getter { get; init; }

	/// <summary>Writes it; null for a property that can't be written (e.g. an action's).</summary>
	internal Action<object?>? Setter { get; init; }

	private string Header => string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;

	public T? GetValue<T>(T? defaultValue)
	{
		if (Value is T { } typedValue)
		{
			return typedValue;
		}

		return defaultValue;
	}

	/// <summary>
	/// Sets the value if it's a valid one: converted to <see cref="ValueType"/> (text is read as <see cref="SettingText"/>
	/// writes it, a number as the setting's kind of number), one of <see cref="AllowedValues"/> (text matched by name,
	/// ignoring case), and within the range. Otherwise leaves it, with why in <paramref name="error"/>, to show to a person
	/// or an agent. Editors, dialogs and agent tools all set values through this.
	/// </summary>
	public bool TrySetValue(object? value, out string? error)
	{
		error = null;
		if (Getter is not null && Setter is null)
		{
			error = $"{Header} can't be changed.";
			return false;
		}

		var type = ValueType ?? Value?.GetType();
		if (value is null)
		{
			if (!AllowsNull)
			{
				error = $"{Header} needs a value.";
				return false;
			}

			Value = null;
			return true;
		}

		if (AllowedValues?.ToList() is { Count: > 0 } allowed)
		{
			var match = value is string text
				? allowed.FirstOrDefault(x => string.Equals(x.ToString(), text, StringComparison.OrdinalIgnoreCase))
				: allowed.FirstOrDefault(x => x.Equals(value));
			if (match is null)
			{
				error = $"\"{SettingText.Format(value)}\" isn't one of {Header}'s values: {string.Join(", ", allowed)}.";
				return false;
			}

			value = match;
		}

		if (type is not null && !type.IsInstanceOfType(value))
		{
			if (!SettingText.TryConvert(value, type, out var converted))
			{
				error = $"\"{SettingText.Format(value)}\" isn't a valid value for {Header}; it takes {SettingText.Describe(type)}.";
				return false;
			}

			value = converted;
		}

		var hasRange = Minimum != double.MinValue || Maximum != double.MaxValue;
		if (hasRange && value is IConvertible and not (string or bool or Enum or DateTime) && Convert.ToDouble(value, CultureInfo.InvariantCulture) is var number
			&& (number < Minimum || number > Maximum))
		{
			error = $"{Header} must be between {Minimum} and {Maximum}.";
			return false;
		}

		Value = value;
		return true;
	}

	/// <inheritdoc cref="TrySetValue(object?, out string?)"/>
	public bool TrySetValue(object? value) => TrySetValue(value, out _);

	/// <summary>A copy to edit without touching this one, e.g. until a dialog is confirmed: its value held, not bound.</summary>
	public DataContainerProperty Clone()
	{
		return new DataContainerProperty
		{
			Name = Name,
			DisplayName = DisplayName,
			Description = Description,
			EditorType = EditorType,
			Value = Value,
			ValueType = ValueType,
			AllowsNull = AllowsNull,
			AllowedValues = AllowedValues,
			IconKey = IconKey,
			Parent = Parent,
			Minimum = Minimum,
			Maximum = Maximum,
		};
	}
}
