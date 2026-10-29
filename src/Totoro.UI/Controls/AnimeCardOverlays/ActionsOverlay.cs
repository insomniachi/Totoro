using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// A card's actions: the ones marked <see cref="CardAction.ShowOnHover"/> as buttons on the cover while it's hovered,
/// one click each, and all of them in the card's right-click menu.
/// Declare the actions inline, or as a shared <see cref="CardActionCollection"/> resource.
/// </summary>
public partial class ActionsOverlay : AnimeCardOverlay<StackPanel>
{
	public ActionsOverlay()
	{
		// Non-null by default so actions can be declared inline with property element syntax.
		Actions = new CardActionCollection();
	}

	[GeneratedDependencyProperty]
	public partial IList<CardAction>? Actions { get; set; }

	protected override void OnAttached()
	{
		base.OnAttached();

		if (AssociatedObject is { } card)
		{
			card.ContextFlyout = CreateMenu();
		}
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();

		if (AssociatedObject is { } card)
		{
			card.ContextFlyout = null;
		}
	}

	protected override StackPanel CreateOverlay()
	{
		var panel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8,
			Margin = new Thickness(8),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
		};
		panel.Resources.MergedDictionaries.Add(new ActionsOverlayResources());

		foreach (var action in (Actions ?? []).Where(x => x.ShowOnHover))
		{
			panel.Children.Add(CreateButton(action));
		}

		Implicit.SetShowAnimations(panel,
		[
			new TranslationAnimation { Duration = s_animationDuration, From = "0, 10, 0", To = "0" },
			new OpacityAnimation { Duration = s_animationDuration, From = 0, To = 1 },
		]);
		Implicit.SetHideAnimations(panel,
		[
			new OpacityAnimation { Duration = s_animationDuration, From = 1, To = 0 },
		]);

		return panel;
	}

	private Button CreateButton(CardAction action)
	{
		// Colours come from ActionsOverlayResources.
		// Made each time the card is hovered, so it's labelled for the anime's place on the list as it is now.
		var (label, icon) = Describe(action);
		var button = new Button
		{
			Content = icon?.CreateIconElement(),
			Width = 40,
			Height = 40,
			Padding = new Thickness(0),
			CornerRadius = new CornerRadius(20),
		};
		button.Click += (_, _) => Invoke(action);

		if (!string.IsNullOrEmpty(label))
		{
			ToolTipService.SetToolTip(button, label);
			AutomationProperties.SetName(button, label);
		}

		return button;
	}

	private MenuFlyout CreateMenu()
	{
		var menu = new MenuFlyout();
		foreach (var action in Actions ?? [])
		{
			var item = new MenuFlyoutItem();
			item.Click += (_, _) => Invoke(action);
			menu.Items.Add(item);
		}

		// Made once per card, which is reused for other anime and whose anime's place on the list changes: labelled on opening.
		menu.Opening += (_, _) =>
		{
			foreach (var (item, action) in menu.Items.Cast<MenuFlyoutItem>().Zip(Actions ?? []))
			{
				var (label, icon) = Describe(action);
				item.Text = label ?? "";
				item.Icon = icon?.CreateIconElement();
			}
		};

		return menu;
	}

	/// <summary>The action's label and icon for the card's anime: its off-list ones while the anime isn't on the user's list.</summary>
	private (string? Label, IconSource? Icon) Describe(CardAction action)
	{
		var offList = AssociatedObject?.Media?.ListStatus is null;
		return offList ? (action.OffListLabel ?? action.Label, action.OffListIcon ?? action.Icon) : (action.Label, action.Icon);
	}

	// The card's anime is read when the action is invoked, not when the button or menu is made: cards are recycled.
	private void Invoke(CardAction action)
	{
		var parameter = action.CommandParameter ?? AssociatedObject?.Media;
		if (action.Command?.CanExecute(parameter) == true)
		{
			action.Command.Execute(parameter);
		}
	}

	private static readonly TimeSpan s_animationDuration = TimeSpan.FromMilliseconds(200);
}
