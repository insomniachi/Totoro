namespace Totoro.Core.Navigation;

/// <summary>A navigator shown over other content, e.g. a side pane, which can be put away.</summary>
public interface IDismissibleNavigator : INavigator
{
	/// <summary>Puts it away (ending what it shows, as when the user dismisses it).</summary>
	void Close();
}
