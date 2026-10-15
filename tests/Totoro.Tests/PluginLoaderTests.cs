using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Plugins;
using Totoro.Core.Settings;
using Xunit;

namespace Totoro.Tests;

public class PluginLoaderTests
{
	[Fact]
	public async Task PluginLoader_LoadsSenshiPlugin_FromDevDirectory()
	{
		var senshiBin = FindPluginBin("Totoro.Plugins.Senshi");
		Assert.True(Directory.Exists(senshiBin), $"Senshi bin directory should exist at {senshiBin}");

		var loader = new PluginLoader();
		var plugins = loader.LoadPlugins([senshiBin]);

		var senshi = Assert.Single(plugins, p => p.Name.Equals("Senshi", StringComparison.OrdinalIgnoreCase));
		Assert.True(senshi.IsLoaded, $"Senshi should be loaded cleanly, but error was: {senshi.LoadError}");
		Assert.NotNull(senshi.Module);
		Assert.Equal("Senshi", senshi.Module.Descriptor.Name);
		Assert.Equal(new Guid("c6066fda-8e30-4545-9599-8e6c3e917312"), senshi.Id);
		Assert.True(senshi.IsDev);

		// Verify module can configure services into DI and resolve IAnimeProvider
		var services = new ServiceCollection();
		services.AddLogging();
		senshi.Module.ConfigureServices(services);
		var sp = services.BuildServiceProvider();

		var provider = sp.GetKeyedService<IAnimeProvider>(senshi.Id);
		Assert.NotNull(provider);
		Assert.Equal("Senshi", provider.Name);

		// Verify live SearchAsync executes and returns results from senshi.to
		var searchResults = await provider.SearchAsync("Frieren", CancellationToken.None);
		Assert.NotEmpty(searchResults);
		Assert.Contains(searchResults, s => s.Title.Contains("Frieren", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void PluginLoader_LoadsSubsPleasePlugin_FromDevDirectory()
	{
		var subsPleaseBin = FindPluginBin("Totoro.Plugins.SubsPlease");
		Assert.True(Directory.Exists(subsPleaseBin), $"SubsPlease bin directory should exist at {subsPleaseBin}");

		var loader = new PluginLoader();
		var plugins = loader.LoadPlugins([subsPleaseBin]);

		var plugin = Assert.Single(plugins, p => p.Name.Equals("SubsPlease", StringComparison.OrdinalIgnoreCase));
		Assert.True(plugin.IsLoaded, $"SubsPlease should be loaded cleanly: {plugin.LoadError}");
		Assert.NotNull(plugin.Module);
		Assert.Equal("SubsPlease", plugin.Module.Descriptor.Name);
		Assert.Equal(new Guid("9b7a71c8-7633-46e8-be5e-288c19ac1330"), plugin.Id);

		var services = new ServiceCollection();
		services.AddLogging();
		plugin.Module.ConfigureServices(services);
		var sp = services.BuildServiceProvider();

		var provider = sp.GetKeyedService<IAnimeProvider>(plugin.Id);
		Assert.NotNull(provider);
		Assert.Equal("SubsPlease", provider.Name);
	}

	[Fact]
	public void PluginLoader_LoadsAnimePahePlugin_FromDevDirectory()
	{
		var animePaheBin = FindPluginBin("Totoro.Plugins.AnimePahe");
		Assert.True(Directory.Exists(animePaheBin), $"AnimePahe bin directory should exist at {animePaheBin}");

		var loader = new PluginLoader();
		var plugins = loader.LoadPlugins([animePaheBin]);

		var plugin = Assert.Single(plugins, p => p.Name.Equals("AnimePahe", StringComparison.OrdinalIgnoreCase));
		Assert.True(plugin.IsLoaded, $"AnimePahe should be loaded cleanly: {plugin.LoadError}");
		Assert.NotNull(plugin.Module);
		Assert.Equal("AnimePahe", plugin.Module.Descriptor.Name);
		Assert.Equal(new Guid("5c2f8e1a-7d43-4b96-a0e5-3f9c1b6d8a27"), plugin.Id);

		var services = new ServiceCollection();
		services.AddLogging();
		plugin.Module.ConfigureServices(services);
		// Register a dummy IWebPageLoader for AnimePaheProvider DI
		services.AddSingleton<Totoro.Core.Web.IWebPageLoader, DummyWebPageLoader>();
		var sp = services.BuildServiceProvider();

		var provider = sp.GetKeyedService<IAnimeProvider>(plugin.Id);
		Assert.NotNull(provider);
		Assert.Equal("AnimePahe", provider.Name);
	}

	[Fact]
	public void PluginLoader_DiscoversAllPlugins_FromPluginsRoot()
	{
		var pluginsRoot = FindPluginsRepoRoot();

		var loader = new PluginLoader();
		var plugins = loader.LoadPlugins([pluginsRoot]);

		Assert.Contains(plugins, p => p.Name.Equals("Senshi", StringComparison.OrdinalIgnoreCase) && p.IsLoaded);
		Assert.Contains(plugins, p => p.Name.Equals("SubsPlease", StringComparison.OrdinalIgnoreCase) && p.IsLoaded);
		Assert.Contains(plugins, p => p.Name.Equals("AnimePahe", StringComparison.OrdinalIgnoreCase) && p.IsLoaded);
	}

	private sealed class DummyWebPageLoader : Totoro.Core.Web.IWebPageLoader
	{
		public Task<Totoro.Core.Web.WebPage> LoadAsync(Uri url, Uri? referrer, CancellationToken ct) =>
			Task.FromResult(new Totoro.Core.Web.WebPage(url, "text/html", ""));
	}

	[Fact]
	public async Task PluginManager_InstallsPackage_FromTotoroPluginZip()
	{
		var packageFile = FindPluginPackage("Senshi");
		Assert.True(File.Exists(packageFile), $"Senshi.totoro-plugin should exist at {packageFile}");

		var mockPrefs = new FakePreferences();
		var loader = new PluginLoader();
		var manager = new PluginManager(loader, mockPrefs);

		var installed = await manager.InstallFromFileAsync(packageFile);

		Assert.NotNull(installed);
		Assert.Equal("Senshi", installed.Name);
		Assert.True(Directory.Exists(installed.DirectoryPath));
		Assert.True(File.Exists(Path.Combine(installed.DirectoryPath, "plugin.json")));
		Assert.True(File.Exists(installed.AssemblyPath));
		Assert.True(manager.RequiresRestart);

		// Clean up installed test directory
		try
		{
			Directory.Delete(installed.DirectoryPath, recursive: true);
		}
		catch
		{
		}
	}

	[Fact]
	public void PluginLoader_HandlesCorruptDirectory_GracefullyWithoutThrowing()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), "Totoro_Corrupt_Plugin_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);
		File.WriteAllText(Path.Combine(tempDir, "plugin.json"), "{ invalid-json }");

		try
		{
			var loader = new PluginLoader();
			var plugins = loader.LoadPlugins([tempDir]);

			var corrupt = Assert.Single(plugins, p => p.DirectoryPath.Equals(tempDir, StringComparison.OrdinalIgnoreCase));
			Assert.False(corrupt.IsLoaded);
			Assert.NotNull(corrupt.LoadError);
		}
		finally
		{
			Directory.Delete(tempDir, recursive: true);
		}
	}

	private static string FindRepoRoot()
	{
		var dir = AppContext.BaseDirectory;
		while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "Totoro.slnx")))
		{
			dir = Path.GetDirectoryName(dir);
		}
		return dir ?? throw new InvalidOperationException("Could not find repository root.");
	}

	private static string FindPluginsRepoRoot()
	{
		var repoRoot = FindRepoRoot();
		var sibling = Path.GetFullPath(Path.Combine(repoRoot, "..", "Totoro.Plugins"));
		if (Directory.Exists(sibling))
		{
			return sibling;
		}

		var localPlugins = Path.Combine(repoRoot, "plugins");
		if (Directory.Exists(localPlugins))
		{
			return localPlugins;
		}

		return sibling;
	}

	private static string FindPluginBin(string pluginName)
	{
		var pluginsRoot = FindPluginsRepoRoot();
		var inSrc = Path.Combine(pluginsRoot, "src", pluginName, "bin", "Debug", "net10.0");
		if (Directory.Exists(inSrc))
		{
			return inSrc;
		}

		var inSrcRelease = Path.Combine(pluginsRoot, "src", pluginName, "bin", "Release", "net10.0");
		if (Directory.Exists(inSrcRelease))
		{
			return inSrcRelease;
		}

		var inLocal = Path.Combine(FindRepoRoot(), "plugins", pluginName, "bin", "Debug", "net10.0");
		if (Directory.Exists(inLocal))
		{
			return inLocal;
		}

		return inSrc;
	}

	private static string FindPluginPackage(string shortName)
	{
		var pluginsRoot = FindPluginsRepoRoot();
		var packagePath = Path.Combine(pluginsRoot, "src", $"Totoro.Plugins.{shortName}", "bin", "Release", "Package", $"{shortName}.totoro-plugin");
		if (File.Exists(packagePath))
		{
			return packagePath;
		}

		return Path.Combine(FindRepoRoot(), "plugins", $"Totoro.Plugins.{shortName}", "bin", "Release", "Package", $"{shortName}.totoro-plugin");
	}

	private sealed class FakePreferences : IPreferences
	{
		private readonly Dictionary<string, object?> _store = [];

		public T? ReadKey<T>(string key, T? defaultValue = default)
		{
			return _store.TryGetValue(key, out var val) && val is T t ? t : defaultValue;
		}

		public void SaveKey<T>(string key, T value)
		{
			_store[key] = value;
		}

		public void RemoveKey(string key)
		{
			_store.Remove(key);
		}
	}
}
