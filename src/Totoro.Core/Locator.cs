using Microsoft.Extensions.DependencyInjection;

namespace Totoro.Core;

public static class Locator
{
	private static IServiceProvider? s_serviceProvider;

	public static void SetServiceProvider(IServiceProvider serviceProvider)
	{
		s_serviceProvider = serviceProvider;
	}

	public static T GetRequiredService<T>()
		where T : notnull
	{
		if (s_serviceProvider == null)
		{
			throw new InvalidOperationException("Service provider is not set.");
		}

		return s_serviceProvider.GetRequiredService<T>();
	}
}
