using Totoro.Core.Settings;

namespace Totoro.Core.Plugins;

public static class PluginPreferences
{
	public const string CatalogUrlKey = "PluginCatalogUrl";
	public const string DefaultCatalogUrl = "https://raw.githubusercontent.com/insomniachi/Totoro.Plugins/main/plugins.json";

	extension(IPreferences preferences)
	{
		/// <summary>The plugins.json the Plugins page lists; empty for the official one.</summary>
		public string PluginCatalogUrl
		{
			get => preferences.ReadKey(CatalogUrlKey, "") is { Length: > 0 } url ? url : DefaultCatalogUrl;
			set => preferences.SaveKey(CatalogUrlKey, string.IsNullOrWhiteSpace(value) || value.Trim() == DefaultCatalogUrl ? "" : value.Trim());
		}
	}
}
