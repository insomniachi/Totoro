using System.ComponentModel;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeDatabase.Anilist;

internal class AnilistModule : IModule<Settings>
{
	public static Guid Id { get; } = new("b5d31e9b-b988-44e8-8e28-348f58cf1d04");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = @"Anilist",
		Description =
			"AniList: The next-generation anime platform Track, share, and discover your favorite anime and manga with AniList. Discover your obsessions. ",
		EntryPoint = "Anlist"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);


		services.AddKeyedTransient<IAnimeTracker, AnilistTracker>(Descriptor.Id);
		services.AddKeyedTransient<IAnimeMetadataProvider, AnilistMetadataProvider>(Descriptor.Id);

		services.AddHttpClient("anilist-client", (sp, client) =>
		{
			var settings = sp.GetRequiredService<IModuleSettings<Settings>>();
			if (settings.Value.Token is { } token)
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
			}
		});

		services.AddTransient(sp =>
		{
			var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("anilist-client");
			return new GraphQLHttpClient("https://graphql.anilist.co/", new NewtonsoftJsonSerializer(), httpClient);
		});
	}
}

public sealed class Settings : DataContainerConfig
{
	public const int ClientId = 10588;

	[IgnoreDataMember]
	public string? Token { get; set; }

	[DisplayName("Include NSFW")]
	public bool IncludeNsfw { get; set; }

	[DisplayName("Search Limit")]
	public double SearchLimit { get; set; } = 15;

	[DisplayName("Title Language")]
	public TitleLanguage TitleLanguage { get; set; } = TitleLanguage.Romaji;
}


public enum TitleLanguage
{
	English,
	Romaji
}