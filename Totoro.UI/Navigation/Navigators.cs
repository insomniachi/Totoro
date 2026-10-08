using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

public static class Navigators
{
	private static readonly Dictionary<string, INavigator> _navigators = [];

	public static INavigator GetNavigator(string key)
	{
		return _navigators[key];
	}

	internal static void AddNavigator(string key, INavigator navigator)
	{
		_navigators[key] = navigator;
	}
}
