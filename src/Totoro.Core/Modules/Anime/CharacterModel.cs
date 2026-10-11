namespace Totoro.Core.Modules.Anime;

public class CharacterModel
{
	public string Name { get; set; } = "";
	public Uri? Image { get; set; }
	/// <summary>HTML, like <see cref="AnimeModel.Description"/>.</summary>
	public string Description { get; set; } = "";
	public string Gender { get; set; } = "";
}
