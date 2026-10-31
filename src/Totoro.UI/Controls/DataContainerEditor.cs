using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using Windows.System;
using Totoro.UI.Input;
using Totoro.Core.Settings;

namespace Totoro.UI.Controls;

/// <summary>
/// Edits a <see cref="DataContainer"/> (e.g. from <see cref="DataContainerConfig.ToDataContainer"/>): a settings card
/// per property, with the editor its EditorType asks for (see <see cref="DataContainerEditors"/>); one with properties
/// under it (see <see cref="ParentAttribute"/>) is an expander with their cards as its items. Edits go straight
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
		var byParent = (newValue ?? []).ToLookup(x => x.Parent ?? "");
		foreach (var property in byParent[""])
		{
			_cards.Children.Add(CreateCard(property, byParent));
		}
	}

	private static FrameworkElement CreateCard(DataContainerProperty property, ILookup<string, DataContainerProperty> byParent)
	{
		var header = string.IsNullOrEmpty(property.DisplayName) ? property.Name : property.DisplayName;
		var editor = CreateEditor(property);
		Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(editor, header);

		if (!byParent[property.Name].Any())
		{
			var card = new SettingsCard { Header = header, Content = editor };
			Describe(card, property, editor, x => card.Description = x, x => card.HeaderIcon = x);
			return card;
		}

		var expander = new SettingsExpander { Header = header, Content = editor };
		// Its chevron takes width the dialog's cards have, which would put the editor under the header (476 by default).
		expander.Resources["SettingsCardWrapThreshold"] = 400d;
		foreach (var child in byParent[property.Name])
		{
			expander.Items.Add(CreateCard(child, byParent));
		}

		Describe(expander, property, editor, x => expander.Description = x, x => expander.HeaderIcon = x);
		return expander;
	}

	/// <summary>Gives a card or expander the property's description and icon, and an action's state.</summary>
	private static void Describe(FrameworkElement card, DataContainerProperty property, FrameworkElement editor, Action<string> setDescription, Action<IconElement> setIcon)
	{
		// Only when there is one: the card declares these as non-null, and leaving them unset is what shows none.
		if (!string.IsNullOrEmpty(property.Description))
		{
			setDescription(property.Description);
		}

		if (Icons.FromKey(property.IconKey) is { } icon)
		{
			setIcon(icon);
		}

		if (editor is Button button && property.Value is SettingsAction action)
		{
			FollowAction(card, button, action, property.Description, setDescription);
		}
	}

	private static FrameworkElement CreateEditor(DataContainerProperty property)
	{
		return property.EditorType switch
		{
			DataContainerEditors.ToggleSwitch => CreateToggleSwitch(property),
			DataContainerEditors.NumberBox => CreateNumberBox(property),
			DataContainerEditors.ComboBox => CreateComboBox(property),
			DataContainerEditors.FileBrowser => CreateFileBrowser(property),
			DataContainerEditors.Password => CreatePasswordBox(property),
			DataContainerEditors.Shortcut => CreateShortcutEditor(property),
			DataContainerEditors.Button when property.Value is SettingsAction action => new Button { MinWidth = 120, Command = action.Command },
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

	private static PasswordBox CreatePasswordBox(DataContainerProperty property)
	{
		var passwordBox = new PasswordBox { MinWidth = 200, MaxWidth = 350 };
		Sync(passwordBox, property, () =>
		{
			// Setting it to what's typed would move the caret.
			if (passwordBox.Password != (property.Value?.ToString() ?? ""))
			{
				passwordBox.Password = property.Value?.ToString() ?? "";
			}
		});
		passwordBox.PasswordChanged += (_, _) => property.Value = passwordBox.Password;
		return passwordBox;
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
			Minimum = property.Minimum,
			Maximum = property.Maximum,
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
		textBox.MaxWidth = 250;
		textBox.PlaceholderText = "No file chosen";

		var browse = new Button { Content = new SymbolIcon(Symbol.More), VerticalAlignment = VerticalAlignment.Top };
		ToolTipService.SetToolTip(browse, "Browse");
		Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(browse, "Browse");
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

	/// <summary>
	/// Keyboard shortcuts as keycaps, as in PowerToys' settings, on a button opening a flyout to change them: a key pressed
	/// while it's open (with any modifiers held) is added, a shortcut clicked is removed. A flyout, not a dialog: settings
	/// are often in a dialog already. The text is <see cref="KeyboardShortcuts"/>'.
	/// </summary>
	private static Button CreateShortcutEditor(DataContainerProperty property)
	{
		// Wrapping, so several shortcuts don't squeeze the card's header.
		var keycaps = new WrapPanel { HorizontalSpacing = 4, VerticalSpacing = 4, MaxWidth = 180 };
		var editing = new WrapPanel { HorizontalSpacing = 4, VerticalSpacing = 4, MinHeight = 32 };
		var clear = new Button { Content = "Clear" };
		var panel = new StackPanel { Spacing = 12, Width = 280 };
		panel.Children.Add(new TextBlock { Text = "Press a key, with Ctrl, Shift or Alt if you like, to add it. Click one to remove it.", TextWrapping = TextWrapping.Wrap });
		panel.Children.Add(editing);
		panel.Children.Add(clear);

		var flyout = new Flyout { Content = panel, Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom };
		var button = new Button { Flyout = flyout, Padding = new Thickness(8, 4, 8, 4) };
		var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		content.Children.Add(keycaps);
		content.Children.Add(new FontIcon { Glyph = "", FontSize = 14 });
		button.Content = content;
		ToolTipService.SetToolTip(button, "Change");

		List<string> GetShortcuts() => KeyboardShortcuts.Split(property.Value?.ToString() ?? "").ToList();
		void SetShortcuts(List<string> shortcuts) => property.Value = string.Join(", ", shortcuts);

		void Show()
		{
			keycaps.Children.Clear();
			editing.Children.Clear();
			var shortcuts = GetShortcuts();
			if (shortcuts.Count == 0)
			{
				keycaps.Children.Add(new TextBlock { Text = "None", VerticalAlignment = VerticalAlignment.Center });
			}

			foreach (var shortcut in shortcuts)
			{
				// Each shortcut's keys together, "or" between shortcuts.
				if (keycaps.Children.Count > 0)
				{
					keycaps.Children.Add(new TextBlock
					{
						Text = "or",
						VerticalAlignment = VerticalAlignment.Center,
						Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
					});
				}

				var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
				foreach (var key in KeyboardShortcuts.GetKeys(shortcut))
				{
					keys.Children.Add(new Border
					{
						Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
						CornerRadius = new CornerRadius(4),
						Padding = new Thickness(8, 2, 8, 2),
						MinWidth = 28,
						Child = new TextBlock
						{
							Text = KeyboardShortcuts.KeyLabel(key),
							FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
							Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"],
							HorizontalAlignment = HorizontalAlignment.Center,
						},
					});
				}

				keycaps.Children.Add(keys);

				var label = KeyboardShortcuts.Label(shortcut);
				var remove = new Button { Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
				var removeContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
				removeContent.Children.Add(new TextBlock { Text = label });
				removeContent.Children.Add(new FontIcon { Glyph = "", FontSize = 10 });
				remove.Content = removeContent;
				Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, $"Remove {label}");
				remove.Click += (_, _) =>
				{
					SetShortcuts(GetShortcuts().Where(x => x != shortcut).ToList());
					// Its button is gone: keep the focus in the flyout, so the keys still reach it.
					clear.Focus(FocusState.Programmatic);
				};
				editing.Children.Add(remove);
			}
		}

		clear.Click += (_, _) => SetShortcuts([]);
		// Every key but those that move around or close the flyout; a modifier waits for the key it's held with.
		panel.PreviewKeyDown += (_, e) =>
		{
			if (e.Key is VirtualKey.Tab or VirtualKey.Escape || KeyboardShortcuts.IsModifier(e.Key))
			{
				return;
			}

			e.Handled = true;
			var shortcuts = GetShortcuts();
			if (KeyboardShortcuts.Format(e.Key) is var shortcut && !shortcuts.Contains(shortcut, StringComparer.OrdinalIgnoreCase))
			{
				SetShortcuts([.. shortcuts, shortcut]);
			}
		};

		Sync(button, property, Show);
		return button;
	}

	/// <summary>Shows the action's label, busy state and description now, and again whenever they change while it's on screen.</summary>
	private static void FollowAction(FrameworkElement card, Button button, SettingsAction action, string? description, Action<string> setDescription)
	{
		var ring = new ProgressRing { Width = 16, Height = 16, IsActive = true };

		void Show()
		{
			button.Content = action.IsBusy ? ring : action.Label;
			button.IsEnabled = !action.IsBusy;
			var text = action.Description is { Length: > 0 } own ? own : description;
			if (!string.IsNullOrEmpty(text))
			{
				setDescription(text);
			}
		}

		void OnChanged(object? sender, PropertyChangedEventArgs e) => Show();

		Show();
		// Subscribed only while loaded, so a long-lived action doesn't keep a discarded editor alive.
		card.Loaded += (_, _) =>
		{
			action.PropertyChanged -= OnChanged;
			action.PropertyChanged += OnChanged;
			Show();
		};
		card.Unloaded += (_, _) => action.PropertyChanged -= OnChanged;
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
