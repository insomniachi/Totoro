namespace Totoro.Core.Modules.Anime;

public class CharacterModel
{
	/// <summary>Its AniList id; 0 when not known.</summary>
	public long Id { get; set; }
	public string Name { get; set; } = "";
	/// <summary>Its name in Japanese; "" when not known.</summary>
	public string NativeName { get; set; } = "";
	public Uri? Image { get; set; }
	/// <summary>HTML, like <see cref="AnimeModel.Description"/>.</summary>
	public string Description { get; set; } = "";
	public string Gender { get; set; } = "";
	public string Age { get; set; } = "";
	/// <summary>The anime it's in, most popular first; only filled in by a character's own details.</summary>
	public IReadOnlyList<AnimeModel> Anime { get; set; } = [];
	/// <summary>Who voices it: in Japanese first, then in each dub; only filled in by a character's own details.</summary>
	public IReadOnlyList<VoiceActorModel> VoiceActors { get; set; } = [];
}

/// <summary>A voice actor, with the characters they've voiced.</summary>
public class VoiceActorModel
{
	/// <summary>Their AniList id.</summary>
	public long Id { get; set; }
	public string Name { get; set; } = "";
	public string NativeName { get; set; } = "";
	public Uri? Image { get; set; }
	/// <summary>HTML, like <see cref="AnimeModel.Description"/>.</summary>
	public string Description { get; set; } = "";
	public string Gender { get; set; } = "";
	public int? Age { get; set; }
	public string HomeTown { get; set; } = "";
	/// <summary>The language they act in, e.g. "Japanese".</summary>
	public string Language { get; set; } = "";
	/// <summary>Who they've voiced, in which anime, most popular anime first; only filled in by their own details.</summary>
	public IReadOnlyList<VoicedRole> Roles { get; set; } = [];
}

public sealed record VoicedRole(CharacterModel Character, AnimeModel Anime);
