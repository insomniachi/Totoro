using Totoro.Core.Helpers;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;
using Xunit;

namespace Totoro.Tests;

/// <summary>
/// Each anime's settings, in the tests' own folder (see <see cref="TestFiles"/>). The store sets the app-wide
/// <see cref="AnimeModel.TitleLanguageOf"/>: it's put back after each test, and these anime have ids no other test uses,
/// so tests running alongside see no difference.
/// </summary>
public sealed class AnimeSettingsStoreTests : IDisposable
{
	private static readonly string s_file = FileHelper.GetPath("anime-settings.json");
	private readonly Func<AnimeModel, TitleLanguage?>? _titleLanguageOf = AnimeModel.TitleLanguageOf;

	public AnimeSettingsStoreTests()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(s_file)!);
		File.Delete(s_file);
	}

	public void Dispose()
	{
		AnimeModel.TitleLanguageOf = _titleLanguageOf;
		File.Delete(s_file);
	}

	private static AnimeModel Anime(long mal, long id = 1, Guid? serviceId = null) =>
		new() { Id = id, ServiceId = serviceId, Title = "Romaji", EngTitle = "English", RomajiTitle = "Romaji", ExternalIds = new AnimeId { MyAnimeList = mal } };

	private static AnimeSettingsStore Create(IPreferences? preferences = null) => new(preferences ?? new PluginLoaderTests.FakePreferences());

	[Fact]
	public void Get_NothingSaved_IsEmpty()
	{
		Assert.True(Create().Get(Anime(880001)).IsEmpty);
	}

	[Fact]
	public void Save_IsKept_AndGivesOutCopies()
	{
		var anime = Anime(880002);
		var provider = Guid.NewGuid();
		var store = Create();
		var settings = new AnimeSettings { ProviderId = provider, OpeningSkipMode = SkipMode.Automatic, EpisodeNumbering = EpisodeNumbering.Absolute };
		settings.Shows[provider] = new ProviderShow("show-1", "Show");

		store.Save(anime, settings);
		settings.HideFromDiscord = true;
		store.Get(anime).EndingSkipMode = SkipMode.Automatic;

		var saved = Create().Get(anime);
		Assert.Equal(provider, saved.ProviderId);
		Assert.Equal(SkipMode.Automatic, saved.OpeningSkipMode);
		Assert.Null(saved.EndingSkipMode);
		Assert.False(saved.HideFromDiscord);
		Assert.Equal(EpisodeNumbering.Absolute, saved.EpisodeNumbering);
		Assert.Equal(new ProviderShow("show-1", "Show"), saved.Shows[provider]);
	}

	[Fact]
	public void Save_Empty_RemovesTheAnime()
	{
		var anime = Anime(880003);
		var store = Create();
		store.Save(anime, new AnimeSettings { HideFromDiscord = true });

		store.Save(anime, new AnimeSettings());

		Assert.DoesNotContain("880003", File.ReadAllText(s_file));
	}

	[Fact]
	public void TitleLanguage_ChangesTheAnimesTitle()
	{
		var anime = Anime(880004);
		var other = Anime(880005);
		var store = Create();

		store.Save(anime, new AnimeSettings { TitleLanguage = TitleLanguage.English });

		Assert.Equal("English", anime.Title);
		Assert.Equal("Romaji", other.Title);
	}

	[Fact]
	public void Get_MovesSettingsFromTheOldDatabaseKey()
	{
		var database = Guid.NewGuid();
		File.WriteAllText(s_file, $$$"""{"{{{database}}}/7":{"HideFromDiscord":true}}""");
		var anime = Anime(880006, id: 7, serviceId: database);

		Assert.True(Create().Get(anime).HideFromDiscord);
		Assert.Contains("\"880006\"", File.ReadAllText(s_file));
	}

	[Fact]
	public void Loads_OldEnumNames_AndSurvivesADamagedFile()
	{
		File.WriteAllText(s_file, """{"880007":{"EpisodeNumbering":"Continued"}}""");
		Assert.Equal(EpisodeNumbering.Absolute, Create().Get(Anime(880007)).EpisodeNumbering);

		File.WriteAllText(s_file, "{ not json");
		Assert.True(Create().Get(Anime(880007)).IsEmpty);
	}

	[Fact]
	public void MovesShowsFromTheOldPreference()
	{
		var database = Guid.NewGuid();
		var provider = Guid.NewGuid();
		var preferences = new PluginLoaderTests.FakePreferences();
		preferences.SaveKey("ProviderShows", new Dictionary<string, ProviderShow>
		{
			[$"{provider}/{database}/8"] = new("show-8", "Eight"),
			["not-a-guid/x/9"] = new("bad", "Bad"),
		});

		var store = Create(preferences);

		Assert.Equal(new ProviderShow("show-8", "Eight"), store.Get(Anime(880008, id: 8, serviceId: database)).Shows[provider]);
		Assert.Null(preferences.ReadKey<Dictionary<string, ProviderShow>>("ProviderShows"));
	}
}
