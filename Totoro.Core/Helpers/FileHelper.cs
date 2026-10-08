
using Totoro.Core.Module;

namespace Totoro.Core.Helpers;

public static class FileHelper
{
	public static string GetModulePath(Descriptor descriptor, string fileName)
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
							"Totoro",
							"Modules",
							descriptor.EntryPoint,
							fileName);
	}

	public static string GetPath(string fileName)
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
							"Totoro",
							fileName);
	}
}