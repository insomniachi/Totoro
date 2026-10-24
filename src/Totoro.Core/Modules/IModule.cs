using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Settings;

namespace Totoro.Core.Modules;

public interface IModule
{
	Descriptor Descriptor { get; }
	void ConfigureServices(IServiceCollection services);
}

public interface IModule<TSettings> : IModule where TSettings : DataContainerConfig
{

}


[DebuggerDisplay("{Name} - {Version}")]
public class Descriptor
{
	public required Guid Id { get; init; }
	public required string Name { get; init; }
	public Version Version { get; } = Assembly.GetCallingAssembly().GetName().Version ?? new Version(1, 0, 0);
	/// <summary>The folder under %LocalAppData%\Totoro\Modules that keeps this module's settings and data. Keep it stable: changing it orphans that data.</summary>
	public required string DataFolder { get; init; }
	public List<string> Components { get; init; } = [];
	public string? Description { get; init; }
	/// <summary>
	/// The module's icon: an image file, relative to the module's assembly (e.g. a plugin's "icon.png"). Read, it's the
	/// full path, or null when the file isn't there (a plugin installed before it had one).
	/// </summary>
	public string? Image { get => field; init => field = value is not null && Path.Combine(_directory, value) is var path && File.Exists(path) ? path : null; }
	private readonly string _directory = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location) is { Length: > 0 } directory ? directory : AppContext.BaseDirectory;
	public Type? SettingViewModel { get; init; }
}
