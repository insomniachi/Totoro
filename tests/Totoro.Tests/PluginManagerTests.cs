using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Totoro.Core;
using Totoro.Core.Helpers;
using Totoro.Core.Plugins;
using Xunit;

namespace Totoro.Tests;

/// <summary>The plugin tests share the plugins folder and the app's version, so they don't run in parallel.</summary>
[CollectionDefinition(Name)]
public sealed class PluginTestCollection
{
	public const string Name = "Plugins";
}

[Collection(PluginTestCollection.Name)]
public class PluginManagerTests
{
	private static string PluginsDir => FileHelper.GetPath("Plugins");

	/// <summary>A package with just a plugin.json, in a folder of its own; deleted with it.</summary>
	private static string CreatePackage(PluginManifest manifest)
	{
		var dir = Path.Combine(Path.GetTempPath(), "Totoro_Test_Package_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(dir, "content"));
		File.WriteAllText(Path.Combine(dir, "content", "plugin.json"), JsonSerializer.Serialize(manifest));
		var package = Path.Combine(dir, "test.totoro-plugin");
		ZipFile.CreateFromDirectory(Path.Combine(dir, "content"), package);
		return package;
	}

	private static PluginManifest Manifest(Guid id, string minAppVersion = "1.0.0") => new()
	{
		Id = id,
		Name = "Test" + id.ToString("N")[..8],
		Version = "1.0.0",
		EntryPoint = "Test.dll",
		MinAppVersion = minAppVersion,
	};

	[Fact]
	public async Task InstallFromFile_RejectsPluginNeedingNewerApp()
	{
		var previous = AppInfo.Version;
		AppInfo.Version = new Version(2, 0, 0);
		var manifest = Manifest(Guid.NewGuid(), minAppVersion: "3.0.0");
		var package = CreatePackage(manifest);
		try
		{
			var manager = new PluginManager(new PluginLoader(), new PluginLoaderTests.FakePreferences());
			var error = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.InstallFromFileAsync(package));
			Assert.Contains("3.0.0", error.Message);
			Assert.False(Directory.Exists(Path.Combine(PluginsDir, manifest.Name)));
		}
		finally
		{
			AppInfo.Version = previous;
			Directory.Delete(Path.GetDirectoryName(package)!, recursive: true);
		}
	}

	[Fact]
	public async Task Annotate_ShowsInstallsAndUninstallsMadeThisSession()
	{
		var manifest = Manifest(Guid.NewGuid());
		var package = CreatePackage(manifest);
		try
		{
			var manager = new PluginManager(new PluginLoader(), new PluginLoaderTests.FakePreferences());
			var item = new PluginCatalogItem { Id = manifest.Id, Name = manifest.Name, Version = "1.1.0", DownloadUrl = "" };

			manager.Annotate(item);
			Assert.False(item.IsInstalled);

			var installed = await manager.InstallFromFileAsync(package);
			manager.Annotate(item);
			Assert.True(item.IsInstalled);
			Assert.Equal("1.0.0", item.InstalledVersion);
			Assert.True(item.HasUpdate);
			Assert.True(manager.RequiresRestart);

			await manager.UninstallAsync(manifest.Id);
			manager.Annotate(item);
			Assert.False(item.IsInstalled);
			Assert.False(Directory.Exists(installed.DirectoryPath));
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(package)!, recursive: true);
		}
	}

	[Fact]
	public async Task Uninstall_RefusesDevPlugin()
	{
		var senshiBin = PluginLoaderTests.FindPluginBin("Totoro.Plugins.Senshi");
		var loader = new PluginLoader();
		var senshi = Assert.Single(loader.LoadPlugins([senshiBin]), p => p.Name == "Senshi");
		var manager = new PluginManager(loader, new PluginLoaderTests.FakePreferences());

		await Assert.ThrowsAsync<InvalidOperationException>(() => manager.UninstallAsync(senshi.Id));
		Assert.True(Directory.Exists(senshiBin));
	}

	[Fact]
	public void LoadPlugins_AppliesStagedUpdate()
	{
		var target = Path.Combine(PluginsDir, "Staged" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(target);
		File.WriteAllText(Path.Combine(target, "old.txt"), "old");
		Directory.CreateDirectory(target + PluginLoader.PendingSuffix);
		File.WriteAllText(Path.Combine(target + PluginLoader.PendingSuffix, "new.txt"), "new");
		try
		{
			new PluginLoader().LoadPlugins();

			Assert.False(Directory.Exists(target + PluginLoader.PendingSuffix));
			Assert.True(File.Exists(Path.Combine(target, "new.txt")));
			Assert.False(File.Exists(Path.Combine(target, "old.txt")));
		}
		finally
		{
			Directory.Delete(target, recursive: true);
		}
	}

	[Fact]
	public void LoadPlugins_SkipsPluginMarkedForDeletion()
	{
		var senshiBin = PluginLoaderTests.FindPluginBin("Totoro.Plugins.Senshi");
		var copy = Path.Combine(PluginsDir, "Senshi");
		Directory.CreateDirectory(copy);
		foreach (var file in Directory.GetFiles(senshiBin))
		{
			File.Copy(file, Path.Combine(copy, Path.GetFileName(file)), overwrite: true);
		}

		File.WriteAllText(Path.Combine(copy, PluginLoader.DeleteMarker), "");
		try
		{
			var plugins = new PluginLoader().LoadPlugins();

			Assert.DoesNotContain(plugins, p => p.Name == "Senshi");
			Assert.False(Directory.Exists(copy));
		}
		finally
		{
			if (Directory.Exists(copy))
			{
				Directory.Delete(copy, recursive: true);
			}
		}
	}

	[Fact]
	public void PluginLoadContext_SharesHostDependencies()
	{
		var senshiBin = PluginLoaderTests.FindPluginBin("Totoro.Plugins.Senshi");
		var senshi = Assert.Single(new PluginLoader().LoadPlugins([senshiBin]), p => p.Name == "Senshi");

		// The host has Flurl, so the plugin uses the host's rather than a copy of its own: the same types on both sides.
		var flurl = senshi.LoadContext!.LoadFromAssemblyName(new AssemblyName("Flurl"));
		Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(flurl));
	}
}
