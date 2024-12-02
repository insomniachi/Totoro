using Flurl;
using Flurl.Http;
using System;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Jellyfin;

internal class Catalog : IAnimeCatalog
{
	class CatalogItem : ICatalogItem, IHaveImage
	{
		required public string Title { get; set; }
		required public string Url { get; set; }
		required public string Image { get; set; }
	}

	public async IAsyncEnumerable<ICatalogItem> Search(string query)
	{
		var response = await ConfigManager<Config>.Current.BaseUrl
			.AppendPathSegment("/Items")
			.SetQueryParams(new 
			{ 
				userId = ConfigManager<Config>.Current.UserId,
				parentId = ConfigManager<Config>.Current.LibraryId,
				searchTerm = query,
				recursive = true,
				includeItemTypes = @"Series"
			})
			.WithHeader("X-Emby-Token", ConfigManager<Config>.Current.ApiKey)
			.GetJsonAsync<Root>();

		foreach (var item in response.Items)
		{
			var seasons = await ConfigManager<Config>.Current.BaseUrl
				.AppendPathSegment($"/Shows/{item.Id}/Seasons")
				.SetQueryParams(new {
					userId = ConfigManager<Config>.Current.UserId,
				})
				.WithHeader("X-Emby-Token", ConfigManager<Config>.Current.ApiKey)
				.GetJsonAsync<Root>();

			var url = ConfigManager<Config>.Current.BaseUrl
				.AppendPathSegment($"/Shows/{item.Id}/Episodes")
				.SetQueryParams(new
				{
					userId = ConfigManager<Config>.Current.UserId,
				});

			if (seasons.Items is not { Count : > 0 })
			{
				yield return new CatalogItem
				{
					Title = item.Name,
					Url = url,
					Image = ConfigManager<Config>.Current.BaseUrl.AppendPathSegment($"/Items/{item.Id}/Images/Primary")
				};
			}

			foreach (var season in seasons.Items)
			{
				yield return new CatalogItem
				{
					Title = $"{item.Name} {(season.IndexNumber > 1 ? season.Name : "")}".Trim(),
					Url = url,
					Image = ConfigManager<Config>.Current.BaseUrl.AppendPathSegment($"/Items/{(season.ImageTags.Primary is null ? item.Id : season.Id)}/Images/Primary")
				};
			}

		}
	}
}
