using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

public static class Navigators
{
	private static readonly Dictionary<string, INavigator> s_navigators = [];

	/// <summary>Raised when a navigator is registered (navigators register when their host control loads).</summary>
	public static event EventHandler<NavigatorAddedEventArgs>? NavigatorAdded;

	public static INavigator GetNavigator(string key)
	{
		return s_navigators[key];
	}

	public static bool TryGetNavigator(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out INavigator? navigator)
	{
		return s_navigators.TryGetValue(key, out navigator);
	}

	internal static void AddNavigator(string key, INavigator navigator)
	{
		s_navigators[key] = navigator;
		NavigatorAdded?.Invoke(null, new NavigatorAddedEventArgs(key, navigator));
	}
}

public sealed class NavigatorAddedEventArgs(string key, INavigator navigator) : EventArgs
{
	public string Key { get; } = key;
	public INavigator Navigator { get; } = navigator;
}
