using Totoro.Core.Modules;

namespace Totoro.Core.Helpers;

public static class FileHelper
{
	/// <summary>Where the app keeps its files: %LocalAppData%\Totoro. Tests move it, to leave the user's own alone.</summary>
	internal static string Root { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Totoro");

	public static string GetModulePath(Descriptor descriptor, string fileName)
	{
		return Path.Combine(Root,
							"Modules",
							descriptor.DataFolder,
							fileName);
	}

	public static string GetPath(string fileName)
	{
		return Path.Combine(Root, fileName);
	}
}
