using System.Runtime.CompilerServices;
using Totoro.Core.Helpers;

namespace Totoro.Tests;

internal static class TestFiles
{
	/// <summary>
	/// The app's files (plugins installed by the tests...) go in a folder of the tests' own, not the user's
	/// %LocalAppData%\Totoro.
	/// </summary>
	[ModuleInitializer]
	internal static void UseOwnFolder()
	{
		FileHelper.Root = Path.Combine(Path.GetTempPath(), "Totoro.Tests", Guid.NewGuid().ToString("N"));
	}
}
