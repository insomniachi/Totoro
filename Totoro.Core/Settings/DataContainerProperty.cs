using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.Core.Settings;

public partial class DataContainerProperty : ObservableObject
{
	public string Name { get; init; } = "";
	public string? DisplayName { get; init; }
	public string? Description { get; init; }
	public string EditorType { get; init; } = "TextBox";
	[ObservableProperty] public partial object? Value { get; set; }
	public IEnumerable<object>? AllowedValues { get; init; }
	public string? IconKey { get; init; }

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
}

