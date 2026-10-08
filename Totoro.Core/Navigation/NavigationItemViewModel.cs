namespace Totoro.Core.Navigation;

public class NavigationItemViewModel
{
	public required Type ViewModelType { get; set; }
	public string Label { get; set; } = "";
	public object? UnselectedIcon { get; set; }
	public object? SelectedIcon { get; set; }
	public int Order { get; set; }
	public bool IsFooter { get; set; }
	public bool IsStaticIcon { get;set; }
}
