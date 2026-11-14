using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Anime;
using Totoro.Modules.AnimeDatabase.Anilist;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>An anime on a character's or voice actor's page: a picture, a title and a line under it; clicking it opens the anime.</summary>
public sealed record PersonCard(string Title, string Subtitle, string? Image, AnimeModel Anime);

/// <summary>A person on another's page, e.g. who voices a character: clicking them opens their own page (<paramref name="Target"/>).</summary>
public sealed record PersonLink(string Name, string Subtitle, string? Image, object Target);

/// <summary>
/// A character or a voice actor, in the side pane (picked in the title bar's search): their picture, names, facts and
/// description, then the anime they're in. Shows what the search gave at once, and the rest once it's loaded.
/// </summary>
public abstract partial class PersonViewModel(ILogger logger) : NavigatableViewModel
{
	[ObservableProperty] public partial string Name { get; protected set; } = "";
	[ObservableProperty] public partial string NativeName { get; protected set; } = "";
	[ObservableProperty] public partial string? Image { get; protected set; }
	/// <summary>A line under the name, e.g. "Japanese · Female · 31 · From Tokyo"; "" when nothing's known.</summary>
	[ObservableProperty] public partial string Facts { get; protected set; } = "";
	/// <summary>HTML; "" when there's none.</summary>
	[ObservableProperty] public partial string Description { get; protected set; } = "";
	[ObservableProperty] public partial IReadOnlyList<PersonCard> Cards { get; protected set; } = [];
	/// <summary>Who voices a character; none on a voice actor's page.</summary>
	[ObservableProperty] public partial IReadOnlyList<PersonLink> People { get; protected set; } = [];
	/// <summary>Why the details couldn't load; "" when they did.</summary>
	[ObservableProperty] public partial string Message { get; private set; } = "";

	/// <summary>The heading over <see cref="Cards"/>.</summary>
	public abstract string CardsHeader { get; }

	protected abstract Task LoadAsync();

	protected override async Task OnNavigatedToInternalAsync()
	{
		try
		{
			await LoadAsync();
			Message = "";
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Failed to load {Name}", Name);
			Message = "Couldn't load the details. Check your connection and try again.";
		}
	}

	protected override Task OnNetworkRestoredAsync() => Message.Length > 0 ? OnNavigatedToInternalAsync() : Task.CompletedTask;

	protected static string Join(params string?[] parts) => string.Join(" · ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed class CharacterViewModel : PersonViewModel
{
	private readonly CharacterModel _character;
	private readonly AnilistPeople _people;

	public CharacterViewModel(CharacterModel character, AnilistPeople people, ILogger<CharacterViewModel> logger) : base(logger)
	{
		_character = character;
		_people = people;
		Show(character);
	}

	public override string CardsHeader => "Appears in";

	protected override async Task LoadAsync()
	{
		// A database without AniList's ids (a plugin's) gives none: what it gave is all there is.
		if (_character.Id > 0)
		{
			Show(await _people.GetCharacterAsync(_character.Id));
		}
	}

	private void Show(CharacterModel character)
	{
		Name = character.Name;
		NativeName = character.NativeName;
		Image = character.Image?.AbsoluteUri;
		Facts = Join(character.Gender, character.Age.Length > 0 ? $"Age {character.Age}" : null);
		Description = character.Description;
		Cards = [.. character.Anime.Select(x => new PersonCard(x.Title, "", x.Image, x))];
		People = [.. character.VoiceActors.Select(x => new PersonLink(x.Name, x.Language, x.Image?.AbsoluteUri, x))];
	}
}

public sealed class VoiceActorViewModel : PersonViewModel
{
	private readonly VoiceActorModel _actor;
	private readonly AnilistPeople _people;

	public VoiceActorViewModel(VoiceActorModel actor, AnilistPeople people, ILogger<VoiceActorViewModel> logger) : base(logger)
	{
		_actor = actor;
		_people = people;
		Show(actor);
	}

	public override string CardsHeader => "Characters voiced";

	protected override async Task LoadAsync() => Show(await _people.GetVoiceActorAsync(_actor.Id));

	private void Show(VoiceActorModel actor)
	{
		Name = actor.Name;
		NativeName = actor.NativeName;
		Image = actor.Image?.AbsoluteUri;
		Facts = Join(actor.Language, actor.Gender, actor.Age is { } age ? $"Age {age}" : null, actor.HomeTown.Length > 0 ? $"From {actor.HomeTown}" : null);
		Description = actor.Description;
		// One card a character: their roles come most popular anime first, so each keeps the series' best-known season.
		Cards = [.. actor.Roles.DistinctBy(x => x.Character.Id).Select(x => new PersonCard(x.Character.Name, x.Anime.Title, x.Character.Image?.AbsoluteUri ?? x.Anime.Image, x.Anime))];
	}
}
