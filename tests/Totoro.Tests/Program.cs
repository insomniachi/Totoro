using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace Totoro.Tests;

public static class Program
{
	public static async Task<int> Main(string[] args)
	{
		Console.WriteLine("=================================================");
		Console.WriteLine(" Running Totoro Player Automation Tests");
		Console.WriteLine("=================================================");

		var testClasses = new[] { typeof(PlayerViewModelTests) };
		var passed = 0;
		var failed = 0;
		var totalStopwatch = Stopwatch.StartNew();

		foreach (var testClass in testClasses)
		{
			Console.WriteLine($"\nSuite: {testClass.Name}");
			var instance = Activator.CreateInstance(testClass);
			var methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.Where(m => m.GetCustomAttribute<FactAttribute>() != null || m.Name.StartsWith("Test_") || m.Name.EndsWith("_Tests") || char.IsUpper(m.Name[0]))
				.ToList();

			foreach (var method in methods)
			{
				var sw = Stopwatch.StartNew();
				try
				{
					var result = method.Invoke(instance, null);
					if (result is Task task)
					{
						await task;
					}

					sw.Stop();
					Console.ForegroundColor = ConsoleColor.Green;
					Console.Write("  [PASS] ");
					Console.ResetColor();
					Console.WriteLine($"{method.Name} ({sw.ElapsedMilliseconds} ms)");
					passed++;
				}
				catch (Exception ex)
				{
					sw.Stop();
					var inner = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
					Console.ForegroundColor = ConsoleColor.Red;
					Console.Write("  [FAIL] ");
					Console.ResetColor();
					Console.WriteLine($"{method.Name} ({sw.ElapsedMilliseconds} ms)");
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine($"         Error: {inner.Message}");
					Console.WriteLine($"         Stack: {inner.StackTrace}");
					Console.ResetColor();
					failed++;
				}
			}
		}

		totalStopwatch.Stop();
		Console.WriteLine("\n-------------------------------------------------");
		Console.WriteLine($"Total: {passed + failed} | Passed: {passed} | Failed: {failed} | Time: {totalStopwatch.ElapsedMilliseconds} ms");
		Console.WriteLine("=================================================");

		return failed == 0 ? 0 : 1;
	}
}
