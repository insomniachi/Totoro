using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeProviders.AnimePahe;

internal class AnimePaheModule : IModule<Settings>
{
	public static Guid Id { get; } = new("5c2f8e1a-7d43-4b96-a0e5-3f9c1b6d8a27");

	public static Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "AnimePahe",
		Description = "AnimePahe streams anime, subtitled and dubbed, from Kwik. Its site is behind Cloudflare, which now and then asks you to show you're not a bot.",
		EntryPoint = "AnimePahe"
	};

	Descriptor IModule.Descriptor => Descriptor;

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		services.AddSingleton<AnimePaheSessions>();
		services.AddKeyedTransient<IAnimeProvider, AnimePaheProvider>(Id);
	}
}

public sealed class Settings : DataContainerConfig
{
	[DisplayName("Site")]
	[Description("AnimePahe's address. It moves now and then; change it if the site has.")]
	public string Site { get; set; } = "https://animepahe.pw";

	[DisplayName("Preferred quality")]
	[Description("Used when the episode has it; otherwise the best there is.")]
	public VideoQuality Quality { get; set; } = VideoQuality.FullHd;

	[DisplayName("Audio")]
	[Description("Used when the episode has it; otherwise the other.")]
	public AudioLanguage Audio { get; set; } = AudioLanguage.Japanese;
}

public enum VideoQuality
{
	[Description("1080p")] FullHd,
	[Description("720p")] Hd,
	[Description("360p")] Sd,
}

public enum AudioLanguage
{
	[Description("Japanese, with subtitles")] Japanese,
	[Description("English (dubbed)")] English,
}

internal static class SettingsExtensions
{
	public static int ToResolution(this VideoQuality quality) => quality switch
	{
		VideoQuality.Hd => 720,
		VideoQuality.Sd => 360,
		_ => 1080,
	};

	/// <summary>How AnimePahe names it (its buttons' data-audio).</summary>
	public static string ToCode(this AudioLanguage audio) => audio == AudioLanguage.English ? "eng" : "jpn";
}
