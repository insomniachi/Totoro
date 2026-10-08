using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Settings;

namespace Totoro.Core.Module;

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
	public string EntryPoint { get; set; } = Assembly.GetCallingAssembly().GetName().Name ?? "";
	public List<string> Components { get; init; } = [];
	public string? Description { get; init; }
	public string? Image { get; init; }
	public Type? SettingViewModel { get; init; }
}
