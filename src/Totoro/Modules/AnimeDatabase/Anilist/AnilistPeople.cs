using GraphQL;
using GraphQL.Client.Http;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeDatabase.Anilist.GraphQl;

namespace Totoro.Modules.AnimeDatabase.Anilist;

/// <summary>
/// Characters and voice actors from AniList, searched by name, and each one's details: the anime a character is in, and
/// who a voice actor has voiced in which anime. Used whichever anime database is selected, as only AniList has them.
/// </summary>
public sealed class AnilistPeople
{
	private readonly GraphQLHttpClient _client;
	private readonly IModuleSettings<Settings> _settings;
	private readonly AnilistLocalList _local;

	// Internal (the list kept on this PC is), so it's registered with a factory: see AnilistModule.
	internal AnilistPeople(GraphQLHttpClient client, IModuleSettings<Settings> settings, AnilistLocalList local)
	{
		_client = client;
		_settings = settings;
		_local = local;
	}

	// Of a voice actor's anime; AniList allows at most 50 a page.
	private const int MaxRoles = 50;

	/// <summary>Characters named like <paramref name="term"/>, best match first, each with the anime it's best known from.</summary>
	public async Task<List<CharacterModel>> SearchCharactersAsync(string term, int count, CancellationToken ct = default)
	{
		var query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
														 .WithCharacters(CharacterBuilder(withDetails: false)
																			 .WithMedia(new MediaConnectionQueryBuilder().WithNodes(AnimeNode()),
																						sort: new List<MediaSort?> { MediaSort.PopularityDesc }, perPage: 1),
																		 search: term, sort: new List<CharacterSort?> { CharacterSort.SearchMatch }),
													 1, count).Build();
		var response = await SendAsync(query, ct);
		return [.. response.Page?.Characters?.Where(x => x is not null).Select(ToCharacter) ?? []];
	}

	/// <summary>
	/// The character with AniList id <paramref name="id"/>: its description and facts, the anime it's in (most popular
	/// first), and who voices it in them.
	/// </summary>
	public async Task<CharacterModel> GetCharacterAsync(long id, CancellationToken ct = default)
	{
		// Edges rather than nodes: each anime with who voices the character in it, in every language it's dubbed in.
		var query = new QueryQueryBuilder().WithCharacter(CharacterBuilder(withDetails: true)
															   .WithMedia(new MediaConnectionQueryBuilder()
																			  .WithEdges(new MediaEdgeQueryBuilder()
																							 .WithNode(AnimeNode())
																							 .WithVoiceActors(new StaffQueryBuilder()
																												  .WithId()
																												  .WithName(new StaffNameQueryBuilder().WithFull().WithNative())
																												  .WithImage(new StaffImageQueryBuilder().WithLarge())
																												  .WithLanguageV2())),
																		  sort: new List<MediaSort?> { MediaSort.PopularityDesc }, type: MediaType.Anime, perPage: MaxRoles),
														   id: (int)id).Build();
		var response = await SendAsync(query, ct);
		return response.Character is { } character ? ToCharacter(character) : throw new InvalidOperationException($"AniList has no character {id}.");
	}

	/// <summary>Voice actors named like <paramref name="term"/>, best match first, each with their best-known role.</summary>
	public async Task<List<VoiceActorModel>> SearchVoiceActorsAsync(string term, int count, CancellationToken ct = default)
	{
		var query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
														 .WithStaff(StaffBuilder(withDetails: false, roles: 1), search: term,
																	sort: new List<StaffSort?> { StaffSort.SearchMatch }),
													 1, count).Build();
		var response = await SendAsync(query, ct);
		// Staff who've voiced no one (directors, artists...) aren't voice actors.
		return [.. response.Page?.Staff?.Where(x => x?.CharacterMedia?.Edges?.Count > 0).Select(ToVoiceActor) ?? []];
	}

	/// <summary>The voice actor with AniList id <paramref name="id"/>: their details, and who they've voiced in which anime.</summary>
	public async Task<VoiceActorModel> GetVoiceActorAsync(long id, CancellationToken ct = default)
	{
		var query = new QueryQueryBuilder().WithStaff(StaffBuilder(withDetails: true, roles: MaxRoles), id: (int)id).Build();
		var response = await SendAsync(query, ct);
		return response.Staff is { } staff ? ToVoiceActor(staff) : throw new InvalidOperationException($"AniList has no voice actor {id}.");
	}

	private async Task<Query> SendAsync(string query, CancellationToken ct)
	{
		var response = await _client.SendQueryAsync<Query>(new GraphQLRequest { Query = query }, ct);
		return response.Errors?.Length > 0 || response.Data is null
			? throw new InvalidOperationException($"AniList couldn't search: {AnilistMetadataProvider.DescribeErrors(response.Errors)}")
			: response.Data;
	}

	// As the anime database's own, with its type to tell anime from manga.
	private static MediaQueryBuilder AnimeNode() => AnilistMetadataProvider.MediaQueryBuilder().WithType();

	private static CharacterQueryBuilder CharacterBuilder(bool withDetails)
	{
		var builder = new CharacterQueryBuilder()
					  .WithId()
					  .WithName(new CharacterNameQueryBuilder().WithFull().WithNative())
					  .WithImage(new CharacterImageQueryBuilder().WithLarge());
		return withDetails ? builder.WithDescription(true).WithGender().WithAge() : builder;
	}

	private static StaffQueryBuilder StaffBuilder(bool withDetails, int roles)
	{
		var builder = new StaffQueryBuilder()
					  .WithId()
					  .WithName(new StaffNameQueryBuilder().WithFull().WithNative())
					  .WithImage(new StaffImageQueryBuilder().WithLarge())
					  .WithLanguageV2()
					  // Each anime, with the characters they voice in it.
					  .WithCharacterMedia(new MediaConnectionQueryBuilder()
											  .WithEdges(new MediaEdgeQueryBuilder()
															 .WithNode(AnimeNode())
															 .WithCharacters(CharacterBuilder(withDetails: false))),
										  sort: new List<MediaSort?> { MediaSort.PopularityDesc }, perPage: roles);
		return withDetails ? builder.WithDescription(true).WithGender().WithAge().WithHomeTown() : builder;
	}

	private CharacterModel ToCharacter(Character character) => new()
	{
		Id = character.Id ?? 0,
		Name = character.Name?.Full ?? "",
		NativeName = character.Name?.Native ?? "",
		Image = ToImage(character.Image?.Large),
		Description = character.Description ?? "",
		Gender = character.Gender ?? "",
		Age = character.Age ?? "",
		// A search gives its anime as nodes, its details as edges (with the voice actors).
		Anime = [.. (character.Media?.Nodes ?? character.Media?.Edges?.Select(x => x?.Node))?.Where(IsShown).Select(x => ToAnime(x!)) ?? []],
		// The original (Japanese) voice first, then the dubs; each once, though they voice it in every season.
		VoiceActors = [.. character.Media?.Edges?.Where(x => IsShown(x?.Node)).SelectMany(x => x.VoiceActors ?? [])
								   .Where(x => x is not null).DistinctBy(x => x.Id)
								   .OrderBy(x => x.LanguageV2 == "Japanese" ? 0 : 1)
								   .Select(x => ToVoiceActor(x)) ?? []],
	};

	private VoiceActorModel ToVoiceActor(StaffData staff) => new()
	{
		Id = staff.Id ?? 0,
		Name = staff.Name?.Full ?? "",
		NativeName = staff.Name?.Native ?? "",
		Image = ToImage(staff.Image?.Large),
		Description = staff.Description ?? "",
		Gender = staff.Gender ?? "",
		Age = staff.Age,
		HomeTown = staff.HomeTown ?? "",
		Language = staff.LanguageV2 ?? "",
		Roles = [.. staff.CharacterMedia?.Edges?.Where(x => IsShown(x?.Node))
						 .SelectMany(x => (x.Characters ?? []).Where(c => c is not null).Select(c => new VoicedRole(ToCharacter(c), ToAnime(x.Node)))) ?? []],
	};

	// Only anime (a voice actor's media can be manga, for a character's other appearances), and adult ones only when asked for.
	private bool IsShown(Media? media) => media is { Type: MediaType.Anime } && (_settings.Value.IncludeNsfw || media.IsAdult is false or null);

	/// <summary>The anime with the user's entry, as AnilistMetadataProvider gives it: AniList's, or while signed out the one kept on this PC.</summary>
	private AnimeModel ToAnime(Media media)
	{
		var anime = AniListModelToAnimeModelConverter.ConvertModel(media);
		if (string.IsNullOrEmpty(_settings.Value.Token))
		{
			anime.Tracking = _local.Get(anime.Id);
		}

		return anime;
	}

	private static Uri? ToImage(string? link) =>
		Uri.TryCreate(link, UriKind.Absolute, out var uri) && !AnilistMetadataProvider.IsPlaceholder(uri) ? uri : null;
}
