using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.Core.Settings;

public partial class DataContainerProperty : ObservableObject
{
	public string Name { get; init; } = "";
	public string? DisplayName { get; init; }
	public string? Description { get; init; }
	public string EditorType { get; init; } = DataContainerEditors.TextBox;
	[ObservableProperty] public partial object? Value { get; set; }
	public IEnumerable<object>? AllowedValues { get; init; }
	public string? IconKey { get; init; }

	/// <summary>The name of the property this one is shown under (see <see cref="ParentAttribute"/>); null for none.</summary>
	public string? Parent { get; init; }

	/// <summary>The lowest and highest a number may be (see RangeAttribute); unbounded by default.</summary>
	public double Minimum { get; init; } = double.MinValue;
	public double Maximum { get; init; } = double.MaxValue;

	public T? GetValue<T>(T? defaultValue)
	{
		if (Value is T { } typedValue)
		{
			return typedValue;
		}

		return defaultValue;
	}

	public object? GetAsType(Type type, object? defaultValue)
	{
		return Value is null ? defaultValue : Convert.ChangeType(Value, type);
	}

	/// <summary>A copy to edit without touching this one, e.g. until a dialog is confirmed.</summary>
	public DataContainerProperty Clone()
	{
		return new DataContainerProperty
		{
			Name = Name,
			DisplayName = DisplayName,
			Description = Description,
			EditorType = EditorType,
			Value = Value,
			AllowedValues = AllowedValues,
			IconKey = IconKey,
			Parent = Parent,
			Minimum = Minimum,
			Maximum = Maximum,
		};
	}
}

