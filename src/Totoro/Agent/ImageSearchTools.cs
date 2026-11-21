using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.Helpers;

namespace Totoro.Agent;

[Description("An anime an image is from, and where in it.")]
public sealed record ImageAnimeMatch(
	AnimeSummary Anime,
	[property: Description("The episode the frame is from, if known.")]
	int? Episode,
	[property: Description("When in the episode the frame is, e.g. \"14:17\".")]
	string At,
	[property: Description("How alike the image and the frame are, out of 100.")]
	int Similarity,
	[property: Description("Where to start the episode to see the scene (a little before it), in seconds: play_episode's startAtSeconds.")]
	int PlayFromSeconds) : IAgentAnime
{
	AnimeModel? IAgentAnime.Source => ((IAgentAnime)Anime).Source;
}

[Description("A character in an image, and the anime it's from.")]
public sealed record ImageCharacter(
	[property: Description("The character's name: in English when the anime's cast has them, else as recognised (often in Japanese).")]
	string Character,
	[property: Description("The work the character is from, as recognised (often in Japanese).")]
	string Work,
	[property: Description("The work as an anime in the user's database; null if it wasn't found there.")]
	AnimeSummary? Anime,
	[property: Description("False when the recognition was unsure.")]
	bool Confident) : IAgentAnime
{
	AnimeModel? IAgentAnime.Source => (Anime as IAgentAnime)?.Source;
}

/// <summary>Agent tools for finding which anime a screenshot is from (see <see cref="ImageSearchService"/>) and who is in it (<see cref="CharacterSearchService"/>).</summary>
public sealed class ImageSearchTools(ImageSearchService search, CharacterSearchService characters)
{
	[AgentTool("find_characters_from_clipboard_image")]
	[Description("Names the characters in the image the user copied (fan art, a collage, a screenshot), left to right, and the anime each is from. Use it when the user asks who is in a picture.")]
	public async Task<IReadOnlyList<ImageCharacter>> FindCharactersFromClipboardAsync()
	{
		if (await ClipboardImage.OpenAsync() is not var (image, contentType))
		{
			throw new AgentToolException("There's no image in the clipboard. Ask the user to copy one.");
		}

		using (image)
		{
			try
			{
				return [.. (await characters.FindAsync(image, contentType)).Select(x => new ImageCharacter(
					x.Character, x.Work, x.Anime is null ? null : AnimeSummary.From(x.Anime), !x.NotConfident))];
			}
			catch (InvalidOperationException ex)
			{
				throw new AgentToolException(ex.Message);
			}
		}
	}

	[AgentTool("find_anime_from_clipboard_image")]
	[Description("Finds which anime, episode and moment the image the user copied (e.g. a screenshot) is from. Best match first; empty if none is close.")]
	public async Task<IReadOnlyList<ImageAnimeMatch>> FindFromClipboardAsync()
	{
		if (await ClipboardImage.OpenAsync() is not var (image, contentType))
		{
			throw new AgentToolException("There's no image in the clipboard. Ask the user to copy one.");
		}

		using (image)
		{
			try
			{
				return [.. (await search.FindAsync(image, contentType)).Select(x => new ImageAnimeMatch(
					AnimeSummary.From(x.Anime), x.Episode, x.At.ToString(x.At.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss", System.Globalization.CultureInfo.InvariantCulture),
					(int)Math.Round(x.Similarity * 100), (int)x.PlayFrom.TotalSeconds))];
			}
			catch (InvalidOperationException ex)
			{
				throw new AgentToolException(ex.Message);
			}
		}
	}
}
