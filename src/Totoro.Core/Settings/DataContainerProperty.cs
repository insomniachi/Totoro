using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.Core.Settings;

public partial class DataContainerProperty : ObservableObject
{
	public string Name { get; init; } = "";
	public string? DisplayName { get; init; }
	public string? Description { get; init; }
	public string EditorType { get; init; } = DataContainerEditors.TextBox;
	[ObservableProperty] public partial object? Value { get; set; }

	/// <summary>
	/// The type the setting holds (a nullable's underlying type), which an editor writes <see cref="Value"/> back as, so
	/// <see cref="DataContainerConfig.UpdateValues"/> can read it; null if unknown (then go by the value's own type).
	/// </summary>
	public Type? ValueType { get; init; }
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
			ValueType = ValueType,
			AllowedValues = AllowedValues,
			IconKey = IconKey,
			Parent = Parent,
			Minimum = Minimum,
			Maximum = Maximum,
		};
	}
}

