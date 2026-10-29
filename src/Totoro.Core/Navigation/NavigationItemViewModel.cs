using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.Core.Navigation;

public partial class NavigationItemViewModel : ObservableObject
{
	public required Type ViewModelType { get; set; }
	public string Label { get; set; } = "";

	/// <summary>What it is navigated to and remembered by (e.g. as the startup page); its label unless two items share one.</summary>
	public string Key { get; set; } = "";

	/// <summary>The mode it shows in; null for both (e.g. Settings).</summary>
	public MediaMode? Mode { get; set; }

	/// <summary>What it's called outside the menu (e.g. choosing the startup page), where each mode's Library is listed: "Library (manga)".</summary>
	public string Name => Mode == MediaMode.Manga ? $"{Label} (manga)" : Label;

	// Icons are IconElements, as objects: Core doesn't depend on WinUI.

	/// <summary>Shown while the item isn't selected (unless it has an <see cref="AnimatedIcon"/>).</summary>
	public object? UnselectedIcon { get; set; }

	/// <summary>Shown while the item is selected (unless it has an <see cref="AnimatedIcon"/>), e.g. a filled version.</summary>
	public object? SelectedIcon { get; set; }

	/// <summary>
	/// An animated icon (an AnimatedIcon), used instead of the other two when there's one: it animates as the item is
	/// pointed at, pressed and selected.
	/// </summary>
	/// <remarks>
	/// Makes a new one for each menu item showing it: the menu's items change with the mode, and an element can't move to
	/// a new item while the old one still holds it.
	/// </remarks>
	public Func<object>? AnimatedIcon { get; set; }

	public bool HasAnimatedIcon => AnimatedIcon is not null;

	public int Order { get; set; }
	public bool IsFooter { get; set; }

	/// <summary>A number shown on the item, e.g. how many downloads are going; none while it's 0.</summary>
	[ObservableProperty]
	public partial int BadgeCount { get; set; }

}
