namespace Totoro.Core.Navigation;

public class NavigationItemViewModel
{
	public required Type ViewModelType { get; set; }
	public string Label { get; set; } = "";

	// Icons are IconElements, as objects: Core doesn't depend on WinUI.

	/// <summary>Shown while the item isn't selected (unless it has an <see cref="AnimatedIcon"/>).</summary>
	public object? UnselectedIcon { get; set; }

	/// <summary>Shown while the item is selected (unless it has an <see cref="AnimatedIcon"/>), e.g. a filled version.</summary>
	public object? SelectedIcon { get; set; }

	/// <summary>
	/// An animated icon (an AnimatedIcon), used instead of the other two when there's one: it animates as the item is
	/// pointed at, pressed and selected.
	/// </summary>
	public object? AnimatedIcon { get; set; }

	public bool HasAnimatedIcon => AnimatedIcon is not null;

	public int Order { get; set; }
	public bool IsFooter { get; set; }
}
