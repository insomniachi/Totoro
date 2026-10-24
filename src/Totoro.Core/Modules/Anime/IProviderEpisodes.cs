namespace Totoro.Core.Modules.Anime;

/// <summary>Which of an anime's aired episodes its anime provider has, for the new episodes badge.</summary>
public interface IProviderEpisodes
{
	/// <summary>
	/// Sets the anime's <see cref="AnimeModel.ProviderEpisodes"/>: at once if it was checked lately, else once its provider
	/// has been asked (in the background).
	/// </summary>
	void Check(AnimeModel anime);
}
