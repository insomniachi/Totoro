using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Totoro.Core.Settings;

namespace Totoro.UI.Controls;

/// <summary>
/// Edits a <see cref="DataContainer"/> (e.g. from <see cref="DataContainerConfig.ToDataContainer"/>): a settings card
/// per property, with the editor its EditorType asks for (see <see cref="DataContainerEditors"/>). Edits go straight
/// to the properties' values, keeping each value's type (an int stays an int), so
/// <see cref="DataContainerConfig.UpdateValues"/> can read them back. For edits that can be cancelled, use the dialog
/// (IDialogService.EditAsync), which edits copies.
/// </summary>
public sealed partial class DataContainerEditor : UserControl
{
	private readonly StackPanel _cards = new() { Spacing = 4 };

	public DataContainerEditor()
	{
		Content = _cards;
		IsTabStop = false;
	}

	[GeneratedDependencyProperty]
	public partial IEnumerable<DataContainerProperty>? Properties { get; set; }

	partial void OnPropertiesChanged(IEnumerable<DataContainerProperty>? newValue)
	{
		_cards.Children.Clear();
		foreach (var property in newValue ?? [])
		{
			_cards.Children.Add(CreateCard(property));
		}
	}

	private static SettingsCard CreateCard(DataContainerProperty property)
	{
		var header = string.IsNullOrEmpty(property.DisplayName) ? property.Name : property.DisplayName;
		var editor = CreateEditor(property);
		Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(editor, header);

		var card = new SettingsCard
		{
			Header = header,
			Content = editor,
		};

		// Only when there is one: the card declares these as non-null, and leaving them unset is what shows none.
		if (!string.IsNullOrEmpty(property.Description))
		{
			card.Description = property.Description;
		}

		if (Icons.FromKey(property.IconKey) is { } icon)
		{
			card.HeaderIcon = icon;
		}

		return card;
	}

	private static FrameworkElement CreateEditor(DataContainerProperty property)
	{
		return property.EditorType switch
		{
			DataContainerEditors.ToggleSwitch => CreateToggleSwitch(property),
			DataContainerEditors.NumberBox => CreateNumberBox(property),
			DataContainerEditors.ComboBox => CreateComboBox(property),
			DataContainerEditors.FileBrowser => CreateFileBrowser(property),
			_ => CreateTextBox(property),
		};
	}

	private static TextBox CreateTextBox(DataContainerProperty property)
	{
		var textBox = new TextBox { MinWidth = 200, MaxWidth = 350, TextWrapping = TextWrapping.Wrap };
		Sync(textBox, property, () => textBox.Text = property.Value?.ToString() ?? "");
		textBox.TextChanged += (_, _) => property.Value = textBox.Text;
		return textBox;
	}

	private static ToggleSwitch CreateToggleSwitch(DataContainerProperty property)
	{
		// The card shows the header; the switch's own On/Off text is enough.
		var toggle = new ToggleSwitch { MinWidth = 0 };
		Sync(toggle, property, () => toggle.IsOn = property.Value is true);
		toggle.Toggled += (_, _) => property.Value = toggle.IsOn;
		return toggle;
	}

	private static NumberBox CreateNumberBox(DataContainerProperty property)
	{
		// Written back as the value's own type: settings read an int property only from an int.
		var type = property.Value?.GetType() ?? typeof(double);
		var isWhole = type != typeof(double) && type != typeof(float) && type != typeof(decimal);

		var numberBox = new NumberBox
		{
			MinWidth = 200,
			SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
			SmallChange = 1,
			LargeChange = 10,
		};
		Sync(numberBox, property, () => numberBox.Value = property.Value is null ? double.NaN : Convert.ToDouble(property.Value));
		numberBox.ValueChanged += (_, args) =>
		{
			if (double.IsNaN(args.NewValue))
			{
				// Cleared: keep the last value rather than writing something that isn't a number.
				numberBox.Value = args.OldValue;
				return;
			}

			var value = isWhole ? Math.Round(args.NewValue) : args.NewValue;
			property.Value = Convert.ChangeType(value, type);
			if (value != args.NewValue)
			{
				numberBox.Value = value;
			}
		};
		return numberBox;
	}

	private static ComboBox CreateComboBox(DataContainerProperty property)
	{
		var values = (property.AllowedValues ?? []).ToList();
		var comboBox = new ComboBox { MinWidth = 200, ItemsSource = values.Select(GetLabel).ToList() };
		Sync(comboBox, property, () => comboBox.SelectedIndex = values.IndexOf(property.Value!));
		comboBox.SelectionChanged += (_, _) =>
		{
			if (comboBox.SelectedIndex >= 0)
			{
				property.Value = values[comboBox.SelectedIndex];
			}
		};
		return comboBox;
	}

	private static Grid CreateFileBrowser(DataContainerProperty property)
	{
		var textBox = CreateTextBox(property);
		textBox.MinWidth = 260;
		textBox.PlaceholderText = "No file chosen";

		var browse = new Button { Content = "Browse…", VerticalAlignment = VerticalAlignment.Top };
		browse.Click += async (_, _) =>
		{
			var picker = new FileOpenPicker(browse.XamlRoot.ContentIslandEnvironment.AppWindowId);
			picker.FileTypeFilter.Add("*");
			if (await picker.PickSingleFileAsync() is { } file)
			{
				property.Value = file.Path;
			}
		};

		var grid = new Grid { ColumnSpacing = 8 };
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		Grid.SetColumn(browse, 1);
		grid.Children.Add(textBox);
		grid.Children.Add(browse);
		return grid;
	}

	/// <summary>Shows the property's value now, and again whenever it changes while the editor is on screen.</summary>
	private static void Sync(FrameworkElement editor, DataContainerProperty property, Action show)
	{
		void OnChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(DataContainerProperty.Value))
			{
				show();
			}
		}

		show();
		// Subscribed only while loaded, so a long-lived property doesn't keep a discarded editor alive.
		editor.Loaded += (_, _) =>
		{
			property.PropertyChanged -= OnChanged;
			property.PropertyChanged += OnChanged;
			show();
		};
		editor.Unloaded += (_, _) => property.PropertyChanged -= OnChanged;
	}

	/// <summary>How a choice is shown: an enum value's [Description], if it has one; otherwise its text.</summary>
	private static string GetLabel(object value)
	{
		if (value is Enum && value.GetType().GetField(value.ToString()!)?.GetCustomAttribute<DescriptionAttribute>() is { } description)
		{
			return description.Description;
		}

		return value.ToString() ?? "";
	}
}
