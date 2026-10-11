using System.ComponentModel;
using System.Runtime.Serialization;
using MalApi;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeDatabase.MyAnimeList;

internal class MyAnimeListModule : IModule<Settings>
{
	public static Guid Id { get; } = new("e6b48ed5-4b76-4a7e-94d1-285c6dd4a125");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "MyAnimeList",
		Description =
			"MyAnimeList, often abbreviated as MAL, is an anime and manga social networking and social cataloging application website run by volunteers. The site provides its users with a list-like system to organize and score anime and manga. It facilitates finding users who share similar tastes and provides a large database on anime and manga",
		EntryPoint = "MyAnimeList"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);

		services.AddSingleton<MyAnimeListSession>();

		services.AddKeyedTransient<IAnimeTracker, MyAnimeListTracker>(Descriptor.Id);
		services.AddKeyedTransient<IAnimeMetadataProvider, MyAnimeListMetadataProvider>(Descriptor.Id);
		services.AddTransient<IAccount, MyAnimeListAccount>();
	}
}

public sealed class Settings : DataContainerConfig
{
	public const string ClientId = "b1d2bd6df963564d1bdb6194ce0a12e9";

	/// <summary>The user's login; refreshed by <see cref="MyAnimeListSession"/> when it expires.</summary>
	[IgnoreDataMember]
	public OAuthToken? Auth { get; set; }

	[DisplayName("Include NSFW")]
	public bool IncludeNsfw { get; set; }

	[DisplayName("Search Limit")]
	public double SearchLimit { get; set; } = 15;
}
