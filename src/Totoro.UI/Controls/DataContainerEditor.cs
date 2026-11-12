using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using Windows.System;
using Totoro.Core;
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
	// In XAML for ThemeResource: brushes looked up in code keep the theme they were looked up in.
	private const string Xmlns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'";
	private const string OrXaml = $"<TextBlock {Xmlns} Text='or' VerticalAlignment='Center' Foreground='{{ThemeResource TextFillColorSecondaryBrush}}' />";
	private const string SwatchXaml = $"<Border {Xmlns} Width='24' Height='24' CornerRadius='4' BorderThickness='1' BorderBrush='{{ThemeResource ControlStrokeColorDefaultBrush}}' />";

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
		var properties = (newValue ?? []).ToList();
		var names = properties.Select(x => x.Name).ToHashSet();
		// A parent that isn't there (or itself) would hide the setting: show it at the top instead.
		var byParent = properties.ToLookup(x => x.Parent is { } parent && parent != x.Name && names.Contains(parent) ? parent : "");
		foreach (var property in byParent[""])
		{
			_cards.Children.Add(CreateCard(property, byParent));
		}
	}

	private static FrameworkElement CreateCard(DataContainerProperty property, ILookup<string, DataContainerProperty> byParent)
	{
		var header = GetHeader(property);
		var editor = CreateEditor(property);
		// A panel isn't what a screen reader lands on: editors made of several controls name their own.
		if (editor is not Panel)
		{
			AutomationProperties.SetName(editor, header);
		}

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

	/// <summary>The app's own editors, one per name in <see cref="DataContainerEditors"/>.</summary>
	private static readonly IPropertyEditor[] s_builtInEditors =
	[
		new PropertyEditor(DataContainerEditors.TextBox, CreateTextBox),
		new PropertyEditor(DataContainerEditors.MultilineText, CreateMultilineTextBox),
		new PropertyEditor(DataContainerEditors.ToggleSwitch, CreateToggleSwitch),
		new PropertyEditor(DataContainerEditors.NumberBox, CreateNumberBox),
		new PropertyEditor(DataContainerEditors.Slider, CreateSlider),
		new PropertyEditor(DataContainerEditors.ComboBox, CreateComboBox),
		new PropertyEditor(DataContainerEditors.Flags, CreateFlags),
		new PropertyEditor(DataContainerEditors.FileBrowser, x => CreatePathBrowser(x, folder: false)),
		new PropertyEditor(DataContainerEditors.FolderBrowser, x => CreatePathBrowser(x, folder: true)),
		new PropertyEditor(DataContainerEditors.Password, CreatePasswordBox),
		new PropertyEditor(DataContainerEditors.DatePicker, CreateDatePicker),
		new PropertyEditor(DataContainerEditors.TimePicker, CreateTimePicker),
		new PropertyEditor(DataContainerEditors.Duration, CreateDurationBox),
		new PropertyEditor(DataContainerEditors.Color, CreateColorPicker),
		new PropertyEditor(DataContainerEditors.Tokens, CreateTokens),
		new PropertyEditor(DataContainerEditors.Shortcut, CreateShortcutEditor),
		new PropertyEditor(DataContainerEditors.Button, x => x.Value is SettingsAction action ? new Button { MinWidth = 120, Command = action.Command } : CreateTextBox(x)),
	];

	/// <summary>The setting's name as shown: its display name, else its name. For editors naming their own controls.</summary>
	public static string GetHeader(DataContainerProperty property) => string.IsNullOrEmpty(property.DisplayName) ? property.Name : property.DisplayName;

	private static FrameworkElement CreateEditor(DataContainerProperty property)
	{
		// Plugins' after the app's, and the last registered wins: a plugin can replace one of the app's editors.
		var editor = s_builtInEditors.Concat(Locator.GetServices<IPropertyEditor>()).LastOrDefault(x => x.EditorType == property.EditorType);
		try
		{
			// No editor of that name (e.g. its plugin was removed): the value's text is still editable.
			return editor?.Create(property) ?? CreateTextBox(property);
		}
		catch (Exception) when (!s_builtInEditors.Contains(editor))
		{
			// A plugin's editor failing mustn't stop its settings from opening.
			return CreateTextBox(property);
		}
	}

	/// <summary>The type to write the value back as: the setting's, else the value's own.</summary>
	private static Type? GetValueType(DataContainerProperty property) => property.ValueType ?? property.Value?.GetType();

	private static TextBox CreateMultilineTextBox(DataContainerProperty property)
	{
		var textBox = CreateTextBox(property);
		textBox.AcceptsReturn = true;
		textBox.MinHeight = 80;
		return textBox;
	}

	/// <summary>A number with both ends of a [Range]: without them a slider has nothing to slide between.</summary>
	private static FrameworkElement CreateSlider(DataContainerProperty property)
	{
		if (property.Minimum == double.MinValue || property.Maximum == double.MaxValue)
		{
			return CreateNumberBox(property);
		}

		var type = GetValueType(property) ?? typeof(double);
		var isWhole = IsWhole(type);
		var slider = new Slider
		{
			MinWidth = 200,
			Minimum = property.Minimum,
			Maximum = property.Maximum,
			StepFrequency = isWhole ? 1 : (property.Maximum - property.Minimum) / 100,
		};
		Sync(slider, property, () => slider.Value = property.Value is null ? property.Minimum : Convert.ToDouble(property.Value));
		slider.ValueChanged += (_, args) => property.Value = Convert.ChangeType(isWhole ? Math.Round(args.NewValue) : args.NewValue, type);
		return slider;
	}

	private static bool IsWhole(Type type) => type != typeof(double) && type != typeof(float) && type != typeof(decimal) && type != typeof(Half);

	/// <summary>A check box for each single value of a [Flags] enum (not 0, nor ones combining others).</summary>
	private static StackPanel CreateFlags(DataContainerProperty property)
	{
		var type = GetValueType(property)!;
		var flags = Enum.GetValues(type).Cast<Enum>()
			.Select(x => (Value: x, Bits: Convert.ToInt64(x)))
			.Where(x => x.Bits > 0 && (x.Bits & (x.Bits - 1)) == 0)
			.Select(x => (x.Value, x.Bits, Box: new CheckBox { Content = GetLabel(x.Value) }))
			.ToList();

		var panel = new StackPanel();
		var showing = false;
		foreach (var (_, _, box) in flags)
		{
			panel.Children.Add(box);
			void Write(object sender, RoutedEventArgs e)
			{
				// Not while showing the value: boxes checked one at a time would write the values in between.
				if (!showing)
				{
					property.Value = Enum.ToObject(type, flags.Where(x => x.Box.IsChecked == true).Aggregate(0L, (all, x) => all | x.Bits));
				}
			}

			box.Checked += Write;
			box.Unchecked += Write;
		}

		Sync(panel, property, () =>
		{
			showing = true;
			var value = property.Value is null ? 0 : Convert.ToInt64(property.Value);
			foreach (var (_, bits, box) in flags)
			{
				box.IsChecked = (value & bits) == bits;
			}

			showing = false;
		});
		return panel;
	}

	private static CalendarDatePicker CreateDatePicker(DataContainerProperty property)
	{
		var type = GetValueType(property) ?? typeof(DateTimeOffset);
		var picker = new CalendarDatePicker { MinWidth = 200 };
		var showing = false;
		Sync(picker, property, () =>
		{
			// Not written back: a DateTime's time of day would be lost just by showing it.
			showing = true;
			// default(...) is year 1, which a DateTimeOffset at a positive offset can't hold: show it as no date.
			picker.Date = property.Value switch
			{
				DateOnly date when date != default => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue)),
				DateTime date when date != default => new DateTimeOffset(date),
				DateTimeOffset date when date != default => date,
				_ => null,
			};
			showing = false;
		});
		picker.DateChanged += (_, args) =>
		{
			if (!showing && args.NewDate is { } date)
			{
				property.Value = type == typeof(DateOnly) ? DateOnly.FromDateTime(date.Date)
					: type == typeof(DateTime) ? date.Date
					: date;
			}
		};
		return picker;
	}

	private static TimePicker CreateTimePicker(DataContainerProperty property)
	{
		var picker = new TimePicker();
		var showing = false;
		Sync(picker, property, () =>
		{
			// Not written back: the picker has no seconds, which would be lost just by showing it.
			showing = true;
			picker.SelectedTime = property.Value is TimeOnly time ? time.ToTimeSpan() : null;
			showing = false;
		});
		picker.SelectedTimeChanged += (_, args) =>
		{
			if (!showing && args.NewTime is { } time)
			{
				property.Value = TimeOnly.FromTimeSpan(time);
			}
		};
		return picker;
	}

	/// <summary>A TimeSpan as text: written back only once it reads as one, so a half-typed value doesn't save.</summary>
	private static TextBox CreateDurationBox(DataContainerProperty property)
	{
		var textBox = new TextBox { MinWidth = 200, PlaceholderText = "h:mm:ss" };
		Sync(textBox, property, () =>
		{
			var value = property.Value as TimeSpan? ?? TimeSpan.Zero;
			// Rewriting what's typed (e.g. "1:5" as 01:05:00) would move the caret.
			if (!TimeSpan.TryParse(textBox.Text, CultureInfo.InvariantCulture, out var typed) || typed != value)
			{
				textBox.Text = value.ToString("c", CultureInfo.InvariantCulture);
			}
		});
		textBox.TextChanged += (_, _) =>
		{
			if (TimeSpan.TryParse(textBox.Text, CultureInfo.InvariantCulture, out var value))
			{
				property.Value = value;
			}
		};
		return textBox;
	}

	/// <summary>A swatch of the color, opening WinUI's color picker.</summary>
	private static DropDownButton CreateColorPicker(DataContainerProperty property)
	{
		var swatch = (Border)XamlReader.Load(SwatchXaml);
		var picker = new ColorPicker { IsAlphaEnabled = true, IsMoreButtonVisible = true };
		var button = new DropDownButton { Content = swatch, Flyout = new Flyout { Content = picker } };
		ToolTipService.SetToolTip(button, "Change");
		Sync(button, property, () =>
		{
			var color = property.Value is System.Drawing.Color value ? value : System.Drawing.Color.Black;
			var shown = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
			swatch.Background = new SolidColorBrush(shown);
			if (picker.Color != shown)
			{
				picker.Color = shown;
			}
		});
		picker.ColorChanged += (_, args) => property.Value = System.Drawing.Color.FromArgb(args.NewColor.A, args.NewColor.R, args.NewColor.G, args.NewColor.B);
		return button;
	}

	/// <summary>A list of strings as removable chips, and a box adding one on Enter. Each change writes a new list.</summary>
	private static StackPanel CreateTokens(DataContainerProperty property)
	{
		var header = GetHeader(property);
		var isArray = GetValueType(property)?.IsArray == true;
		var chips = new WrapPanel { HorizontalSpacing = 4, VerticalSpacing = 4 };
		var input = new TextBox { PlaceholderText = "Type one, then press Enter" };
		AutomationProperties.SetName(input, header);
		var panel = new StackPanel { Spacing = 8, MinWidth = 200, MaxWidth = 350 };
		panel.Children.Add(chips);
		panel.Children.Add(input);

		List<string> Get() => (property.Value as IEnumerable<string>)?.ToList() ?? [];
		// A new list, never the one there: the dialog's copies share it with the settings until saved.
		void Set(List<string> items) => property.Value = isArray ? items.ToArray() : items;

		Sync(panel, property, () =>
		{
			chips.Children.Clear();
			foreach (var item in Get())
			{
				var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
				content.Children.Add(new TextBlock { Text = item });
				content.Children.Add(new FontIcon { Glyph = "", FontSize = 10 });
				var remove = new Button { Content = content };
				AutomationProperties.SetName(remove, $"Remove {item}");
				remove.Click += (_, _) =>
				{
					Set(Get().Where(x => x != item).ToList());
					// Its button is gone: keep the focus here.
					input.Focus(FocusState.Programmatic);
				};
				chips.Children.Add(remove);
			}

			chips.Visibility = chips.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
		});
		input.KeyDown += (_, e) =>
		{
			if (e.Key == VirtualKey.Enter && input.Text.Trim() is { Length: > 0 } text)
			{
				e.Handled = true;
				if (!Get().Contains(text))
				{
					Set([.. Get(), text]);
				}

				input.Text = "";
			}
		};
		return panel;
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
		// Written back as the setting's own type: settings read an int property only from an int.
		var type = GetValueType(property) ?? typeof(double);
		var isWhole = IsWhole(type);

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
			try
			{
				property.Value = Convert.ChangeType(value, type);
			}
			catch (OverflowException)
			{
				// Too big for the value's type (e.g. an int without a [Range]): keep the last value.
				numberBox.Value = args.OldValue;
				return;
			}

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

	/// <summary>A file's or folder's path: typed, or picked with the button beside it.</summary>
	private static Grid CreatePathBrowser(DataContainerProperty property, bool folder)
	{
		var header = GetHeader(property);
		var textBox = CreateTextBox(property);
		textBox.MaxWidth = 250;
		textBox.PlaceholderText = folder ? "No folder chosen" : "No file chosen";
		AutomationProperties.SetName(textBox, header);

		var browse = new Button { Content = new SymbolIcon(Symbol.More), VerticalAlignment = VerticalAlignment.Top };
		ToolTipService.SetToolTip(browse, "Browse");
		AutomationProperties.SetName(browse, "Browse");
		browse.Click += async (_, _) =>
		{
			var window = browse.XamlRoot.ContentIslandEnvironment.AppWindowId;
			if (folder)
			{
				if (await new FolderPicker(window).PickSingleFolderAsync() is { } picked)
				{
					property.Value = picked.Path;
				}

				return;
			}

			var picker = new FileOpenPicker(window);
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
					keycaps.Children.Add((TextBlock)XamlReader.Load(OrXaml));
				}

				var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
				foreach (var key in KeyboardShortcuts.GetKeys(shortcut))
				{
					keys.Children.Add(new DevWinUI.KeyVisual { Content = KeyboardShortcuts.KeyLabel(key), VisualType = DevWinUI.VisualType.Small });
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
			// Null (the card's default, no description) when there's none, so an earlier action's text doesn't stay.
			setDescription(string.IsNullOrEmpty(text) ? null! : text);
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

	/// <summary>
	/// Shows the property's value now (with <paramref name="show"/>), and again whenever it changes while the editor is
	/// on screen; for a plugin's editors too (see <see cref="IPropertyEditor"/>).
	/// </summary>
	public static void Sync(FrameworkElement editor, DataContainerProperty property, Action show)
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
