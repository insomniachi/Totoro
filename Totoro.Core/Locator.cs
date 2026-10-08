using Microsoft.Extensions.DependencyInjection;

namespace Totoro.Core;

public static class Locator
{
	private static IServiceProvider? _serviceProvider;

	public static void SetServiceProvider(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public static T GetRequiredService<T>()
		where T : notnull
	{
		if (_serviceProvider == null)
		{
			throw new InvalidOperationException("Service provider is not set.");
		}

		return _serviceProvider.GetRequiredService<T>();
	}
}
